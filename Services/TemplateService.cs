using AutoMapper;
using FeedBackGeneratorApp.DTOs;
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
            var template = _mapper.Map<SurveyTemplate>(dto);
            template.CreatedAt = DateTime.UtcNow;

            await _templateRepo.AddAsync(template);
            return _mapper.Map<TemplateResponseDto>(template);
        }

        public async Task<TemplateResponseDto?> GetTemplateByIdAsync(int id)
        {
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
            var template = await _templateRepo.GetByIdAsync(id);
            if (template == null) return false;

            await _templateRepo.DeleteAsync(template);
            return true;
        }
    }
}
