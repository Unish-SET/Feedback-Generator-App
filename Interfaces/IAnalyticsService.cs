using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface IAnalyticsService
    {
        Task<SurveyAnalyticsDto?> GetSurveyAnalyticsAsync(int surveyId);
    }
}
