using AutoMapper;
using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Services
{
    public class DistributionService : IDistributionService
    {
        private readonly FeedbackDbContext _context;
        private readonly IRepository<SurveyDistribution> _distributionRepo;
        private readonly IMapper _mapper;

        public DistributionService(FeedbackDbContext context, IRepository<SurveyDistribution> distributionRepo, IMapper mapper)
        {
            _context = context;
            _distributionRepo = distributionRepo;
            _mapper = mapper;
        }

        public async Task<DistributionResponseDto> CreateDistributionAsync(CreateDistributionDto dto)
        {
            if (dto.SurveyId <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");

            // Verify survey exists
            var surveyExists = await _context.Surveys.AnyAsync(s => s.Id == dto.SurveyId);
            if (!surveyExists)
                throw new NotFoundException($"Survey with ID {dto.SurveyId} was not found.");

            // Validate distribution type
            var validTypes = new[] { "Link", "Email", "QRCode" };
            if (string.IsNullOrWhiteSpace(dto.DistributionType))
                throw new BadRequestException("Distribution type is required.");

            if (!validTypes.Contains(dto.DistributionType))
                throw new BadRequestException($"Invalid distribution type '{dto.DistributionType}'. Valid types are: {string.Join(", ", validTypes)}.");

            // Validate email distribution has a value
            if (dto.DistributionType == "Email" && string.IsNullOrWhiteSpace(dto.DistributionValue))
                throw new BadRequestException("Email distribution requires a recipient email address in the DistributionValue field.");

            var distribution = _mapper.Map<SurveyDistribution>(dto);
            distribution.CreatedAt = DateTime.UtcNow;

            // Auto-generate link if type is Link and no value provided
            if (dto.DistributionType == "Link" && string.IsNullOrEmpty(dto.DistributionValue))
            {
                distribution.DistributionValue = $"/survey/respond/{dto.SurveyId}?token={Guid.NewGuid()}";
            }

            // Auto-generate QR data if type is QRCode
            if (dto.DistributionType == "QRCode" && string.IsNullOrEmpty(dto.DistributionValue))
            {
                distribution.DistributionValue = $"/survey/respond/{dto.SurveyId}?token={Guid.NewGuid()}";
            }

            if (dto.ScheduledAt == null || dto.ScheduledAt <= DateTime.UtcNow)
            {
                distribution.SentAt = DateTime.UtcNow;
            }

            await _distributionRepo.AddAsync(distribution);

            var result = await _context.SurveyDistributions
                .Include(d => d.Survey)
                .FirstOrDefaultAsync(d => d.Id == distribution.Id);

            return _mapper.Map<DistributionResponseDto>(result);
        }

        public async Task<IEnumerable<DistributionResponseDto>> GetDistributionsBySurveyAsync(int surveyId)
        {
            if (surveyId <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");

            // Verify survey exists
            var surveyExists = await _context.Surveys.AnyAsync(s => s.Id == surveyId);
            if (!surveyExists)
                throw new NotFoundException($"Survey with ID {surveyId} was not found.");

            var distributions = await _context.SurveyDistributions
                .Include(d => d.Survey)
                .Where(d => d.SurveyId == surveyId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<DistributionResponseDto>>(distributions);
        }

        public async Task<bool> DeleteDistributionAsync(int id)
        {
            if (id <= 0)
                throw new BadRequestException("Distribution ID must be a positive number.");

            var distribution = await _distributionRepo.GetByIdAsync(id);
            if (distribution == null)
                throw new NotFoundException($"Distribution with ID {id} was not found.");

            await _distributionRepo.DeleteAsync(distribution);
            return true;
        }
    }
}
