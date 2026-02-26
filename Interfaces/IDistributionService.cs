using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface IDistributionService
    {
        Task<DistributionResponseDto> CreateDistributionAsync(CreateDistributionDto dto);
        Task<IEnumerable<DistributionResponseDto>> GetDistributionsBySurveyAsync(int surveyId);
        Task<bool> DeleteDistributionAsync(int id);
    }
}
