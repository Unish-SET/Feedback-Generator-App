using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface ISurveyService
    {
        Task<SurveyResponseDto> CreateSurveyAsync(CreateSurveyDto dto, int userId);
        Task<SurveyResponseDto?> GetSurveyByIdAsync(int id);
        Task<PagedResult<SurveyResponseDto>> GetAllSurveysAsync(PaginationParams paginationParams);
        Task<PagedResult<SurveyResponseDto>> GetSurveysByUserAsync(int userId, PaginationParams paginationParams);
        Task<SurveyResponseDto?> UpdateSurveyAsync(int id, UpdateSurveyDto dto);
        Task<bool> DeleteSurveyAsync(int id);
        Task<QuestionResponseDto> AddQuestionAsync(int surveyId, CreateQuestionDto dto);
        Task<bool> DeleteQuestionAsync(int questionId);
    }
}
