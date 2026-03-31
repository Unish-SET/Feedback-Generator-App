namespace FeedBackApp.Interfaces
{
    public interface IExcelService
    {
        Task<byte[]> ExportExcelAsync(int surveyId, int userId, string role);
    }
}
