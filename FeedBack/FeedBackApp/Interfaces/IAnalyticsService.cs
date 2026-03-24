using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IAnalyticsService
    {
        Task<SurveyAnalyticsDto> GetAnalyticsAsync(int surveyId, int userId, string role);
    }
}
