namespace FeedBackApp.Interfaces
{
    public interface IExportService
    {
        Task<byte[]> ExportCsvAsync(int surveyId, int userId, string role);
    }
}
