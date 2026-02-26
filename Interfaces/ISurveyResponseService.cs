using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface ISurveyResponseService
    {
        Task<SurveyResponseDetailDto> SubmitResponseAsync(SubmitResponseDto dto, int? userId);
        Task<SurveyResponseDetailDto?> GetResponseByIdAsync(int id);
        Task<PagedResult<SurveyResponseDetailDto>> GetResponsesBySurveyAsync(int surveyId, PaginationParams paginationParams);
        Task<SurveyResponseDetailDto> PauseResponseAsync(int responseId);
        Task<SurveyResponseDetailDto> ResumeResponseAsync(int responseId, List<SubmitAnswerDto> additionalAnswers);
    }
}
