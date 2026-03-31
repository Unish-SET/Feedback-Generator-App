using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IAdminSurveyService
    {
        Task<PaginatedResult<AdminSurveyListDto>> GetAllSurveysAsync(AdminSurveyFilterParams filter);
        Task<AdminSurveyDetailDto>                GetSurveyDetailAsync(int surveyId);
        Task                                      SoftDeleteSurveyAsync(int surveyId, int adminId, string? ip, string? correlationId);
        Task                                      RestoreSurveyAsync(int surveyId, int adminId, string? ip, string? correlationId);
        Task                                      SetSurveyStatusAsync(int surveyId, string status, int adminId);
        Task<AdminStatsDto>                       GetStatsAsync();
    }
}
