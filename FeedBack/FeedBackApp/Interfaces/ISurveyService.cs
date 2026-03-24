using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface ISurveyService
    {
        Task<SurveyResponseDto>              CreateAsync(CreateSurveyDto dto, int userId);
        Task<SurveyResponseDto>              UpdateAsync(int surveyId, UpdateSurveyDto dto, int userId, string role);
        Task<SurveyResponseDto>              GetByIdAsync(int surveyId, int userId, string role);
        Task<PaginatedResult<SurveyListDto>> GetAllAsync(PaginationParams pagination, int userId, string role);
        Task<SurveyResponseDto>              PublishAsync(int surveyId, int userId, string role);
        Task<SurveyResponseDto>              UnpublishAsync(int surveyId, int userId, string role);
        Task<SurveyResponseDto>              CloseAsync(int surveyId, int userId, string role);
        Task                                 DeleteAsync(int surveyId, int userId, string role);
        Task<PublicSurveyDto>                GetByPublicTokenAsync(Guid publicToken);
        Task                                 CloneQuestionsAsync(int sourceSurveyId, int targetSurveyId, int userId, string role);
    }
}
