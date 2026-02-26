using AutoMapper;
using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Services
{
    public class SurveyService : ISurveyService
    {
        private readonly FeedbackDbContext _context;
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IRepository<Question> _questionRepo;
        private readonly IRepository<SurveyDistribution> _distributionRepo;
        private readonly IMapper _mapper;

        public SurveyService(FeedbackDbContext context, IRepository<Survey> surveyRepo, IRepository<Question> questionRepo, IRepository<SurveyDistribution> distributionRepo, IMapper mapper)
        {
            _context = context;
            _surveyRepo = surveyRepo;
            _questionRepo = questionRepo;
            _distributionRepo = distributionRepo;
            _mapper = mapper;
        }

        public async Task<SurveyResponseDto> CreateSurveyAsync(CreateSurveyDto dto, int userId)
        {
            var survey = _mapper.Map<Survey>(dto);
            survey.CreatedByUserId = userId;
            survey.CreatedAt = DateTime.UtcNow;
            survey.UpdatedAt = DateTime.UtcNow;

            await _surveyRepo.AddAsync(survey);

            if (dto.Questions != null && dto.Questions.Any())
            {
                foreach (var qDto in dto.Questions)
                {
                    var question = _mapper.Map<Question>(qDto);
                    question.SurveyId = survey.Id;
                    await _questionRepo.AddAsync(question);
                }
            }

            // Auto-generate shareable link
            var distribution = new SurveyDistribution
            {
                SurveyId = survey.Id,
                DistributionType = "Link",
                DistributionValue = $"/survey/respond/{survey.Id}?token={Guid.NewGuid()}",
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _distributionRepo.AddAsync(distribution);

            return await GetSurveyByIdAsync(survey.Id) ?? throw new Exception("Failed to create survey.");
        }

        public async Task<SurveyResponseDto?> GetSurveyByIdAsync(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.CreatedByUser)
                .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
                .Include(s => s.SurveyDistributions)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (survey == null) return null;

            var dto = _mapper.Map<SurveyResponseDto>(survey);
            var link = survey.SurveyDistributions.FirstOrDefault(d => d.DistributionType == "Link");
            if (link != null) dto.ShareableLink = link.DistributionValue;

            return dto;
        }

        public async Task<PagedResult<SurveyResponseDto>> GetAllSurveysAsync(PaginationParams paginationParams)
        {
            var query = _context.Surveys
                .Include(s => s.CreatedByUser)
                .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
                .AsQueryable();

            // Filter by search term
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(s => s.Title.ToLower().Contains(search)
                    || s.Description.ToLower().Contains(search));
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "title" => paginationParams.SortDescending ? query.OrderByDescending(s => s.Title) : query.OrderBy(s => s.Title),
                "createdat" => paginationParams.SortDescending ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
                _ => query.OrderByDescending(s => s.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var surveys = await query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToListAsync();

            return new PagedResult<SurveyResponseDto>
            {
                Items = _mapper.Map<List<SurveyResponseDto>>(surveys),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<PagedResult<SurveyResponseDto>> GetSurveysByUserAsync(int userId, PaginationParams paginationParams)
        {
            var query = _context.Surveys
                .Include(s => s.CreatedByUser)
                .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
                .Where(s => s.CreatedByUserId == userId)
                .AsQueryable();

            // Filter
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(s => s.Title.ToLower().Contains(search)
                    || s.Description.ToLower().Contains(search));
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "title" => paginationParams.SortDescending ? query.OrderByDescending(s => s.Title) : query.OrderBy(s => s.Title),
                _ => query.OrderByDescending(s => s.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var surveys = await query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToListAsync();

            return new PagedResult<SurveyResponseDto>
            {
                Items = _mapper.Map<List<SurveyResponseDto>>(surveys),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<SurveyResponseDto?> UpdateSurveyAsync(int id, UpdateSurveyDto dto)
        {
            var survey = await _surveyRepo.GetByIdAsync(id);
            if (survey == null) return null;

            if (dto.Title != null) survey.Title = dto.Title;
            if (dto.Description != null) survey.Description = dto.Description;
            if (dto.IsActive.HasValue) survey.IsActive = dto.IsActive.Value;
            if (dto.BrandingConfig != null) survey.BrandingConfig = dto.BrandingConfig;

            survey.Version++;
            survey.UpdatedAt = DateTime.UtcNow;

            await _surveyRepo.UpdateAsync(survey);
            return await GetSurveyByIdAsync(id);
        }

        public async Task<bool> DeleteSurveyAsync(int id)
        {
            var survey = await _surveyRepo.GetByIdAsync(id);
            if (survey == null) return false;

            await _surveyRepo.DeleteAsync(survey);
            return true;
        }

        public async Task<QuestionResponseDto> AddQuestionAsync(int surveyId, CreateQuestionDto dto)
        {
            var question = _mapper.Map<Question>(dto);
            question.SurveyId = surveyId;

            await _questionRepo.AddAsync(question);
            return _mapper.Map<QuestionResponseDto>(question);
        }

        public async Task<bool> DeleteQuestionAsync(int questionId)
        {
            var question = await _questionRepo.GetByIdAsync(questionId);
            if (question == null) return false;

            await _questionRepo.DeleteAsync(question);
            return true;
        }
    }
}
