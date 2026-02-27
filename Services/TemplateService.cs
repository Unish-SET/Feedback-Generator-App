using AutoMapper;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Services
{
    public class TemplateService : ITemplateService
    {
        private readonly IRepository<SurveyTemplate> _templateRepo;
        private readonly IMapper _mapper;

        public TemplateService(IRepository<SurveyTemplate> templateRepo, IMapper mapper)
        {
            _templateRepo = templateRepo;
            _mapper = mapper;
        }

        public async Task<TemplateResponseDto> CreateTemplateAsync(CreateTemplateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new BadRequestException("Template name is required.");

            if (string.IsNullOrWhiteSpace(dto.TemplateData))
                throw new BadRequestException("Template data (JSON blueprint) is required.");

            // Check for duplicate name
            var existing = await _templateRepo.FindAsync(t => t.Name == dto.Name);
            if (existing.Any())
                throw new ConflictException($"A template with the name '{dto.Name}' already exists.");

            var template = _mapper.Map<SurveyTemplate>(dto);
            template.CreatedAt = DateTime.UtcNow;

            await _templateRepo.AddAsync(template);
            return _mapper.Map<TemplateResponseDto>(template);
        }

        public async Task<TemplateResponseDto?> GetTemplateByIdAsync(int id)
        {
            if (id <= 0)
                throw new BadRequestException("Template ID must be a positive number.");

            var template = await _templateRepo.GetByIdAsync(id);
            return template == null ? null : _mapper.Map<TemplateResponseDto>(template);
        }

        public async Task<IEnumerable<TemplateResponseDto>> GetAllTemplatesAsync()
        {
            var templates = await _templateRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<TemplateResponseDto>>(templates);
        }

        public async Task<bool> DeleteTemplateAsync(int id)
        {
            if (id <= 0)
                throw new BadRequestException("Template ID must be a positive number.");

            var template = await _templateRepo.GetByIdAsync(id);
            if (template == null)
                throw new NotFoundException($"Template with ID {id} was not found.");

            await _templateRepo.DeleteAsync(template);
            return true;
        }
    }
}
