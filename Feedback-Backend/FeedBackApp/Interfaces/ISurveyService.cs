using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface ISurveyService
    {
        Task<SurveyResponseDto>              CreateAsync(CreateSurveyDto dto, int userId);
        Task<SurveyResponseDto>              UpdateAsync(int surveyId, UpdateSurveyDto dto, int userId, string role);
        Task<SurveyResponseDto>              GetByIdAsync(int surveyId, int userId, string role);
        Task<PaginatedResult<SurveyListDto>> GetAllAsync(SurveyFilterParams filter, int userId, string role);
        Task<SurveyResponseDto>              SetStateAsync(int surveyId, SetSurveyStateDto dto, int userId, string role);
        Task                                 DeleteAsync(int surveyId, int userId, string role);
        Task<PublicSurveyDto>                GetByPublicTokenAsync(Guid publicToken);
        Task                                 CloneQuestionsAsync(int sourceSurveyId, int targetSurveyId, int userId, string role);
    }
}
