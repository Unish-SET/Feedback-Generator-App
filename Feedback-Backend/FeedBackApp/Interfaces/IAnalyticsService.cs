using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IAnalyticsService
    {
        Task<SurveyAnalyticsDto> GetAnalyticsAsync(int surveyId, int userId, string role, AnalyticsFilterParams? filter = null);
        Task<(string Html, string Title)> BuildReportHtmlAsync(int surveyId, int userId, string role);
    }
}
