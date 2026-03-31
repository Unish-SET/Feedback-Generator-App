using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IQuestionBankService
    {
        Task<BankQuestionDto>                    CreateAsync(CreateBankQuestionDto dto, int userId);
        Task<BankQuestionDto>                    UpdateAsync(int id, UpdateBankQuestionDto dto, int userId, string role);
        Task                                     DeleteAsync(int id, int userId, string role);
        Task<BankQuestionDto>                    GetByIdAsync(int id, int userId, string role);
        Task<PaginatedResult<BankQuestionDto>>   GetAllAsync(BankQuestionFilterParams filter, int userId, string role);
        Task<CloneFromBankResultDto>             CloneIntoSurveyAsync(CloneFromBankDto dto, int userId, string role);
        Task                                     AutoSaveQuestionsAsync(IEnumerable<Question> questions, int userId);
    }
}
