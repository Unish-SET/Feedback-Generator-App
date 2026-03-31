using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IQuestionService
    {
        Task<QuestionResponseDto>       AddQuestionAsync(int surveyId, CreateQuestionDto dto, int userId, string role);
        Task<QuestionResponseDto>       UpdateQuestionAsync(int surveyId, int questionId, UpdateQuestionDto dto, int userId, string role);
        Task                            DeleteQuestionAsync(int surveyId, int questionId, int userId, string role);
        Task<List<QuestionResponseDto>> GetQuestionsAsync(int surveyId, int userId, string role, string? typeFilter = null);
    }
}
