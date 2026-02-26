using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface ITemplateService
    {
        Task<TemplateResponseDto> CreateTemplateAsync(CreateTemplateDto dto);
        Task<TemplateResponseDto?> GetTemplateByIdAsync(int id);
        Task<IEnumerable<TemplateResponseDto>> GetAllTemplatesAsync();
        Task<bool> DeleteTemplateAsync(int id);
    }
}
