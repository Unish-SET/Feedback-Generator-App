using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface IExportService
    {
        byte[] ExportAnalyticsToCsv(SurveyAnalyticsDto analytics);
        byte[] ExportAnalyticsToExcel(SurveyAnalyticsDto analytics);
    }
}
