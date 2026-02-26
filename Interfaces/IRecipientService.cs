using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface IRecipientService
    {
        Task<RecipientResponseDto> AddRecipientAsync(CreateRecipientDto dto, int userId);
        Task<PagedResult<RecipientResponseDto>> GetAllRecipientsAsync(int userId, PaginationParams paginationParams);
        Task<PagedResult<RecipientResponseDto>> GetRecipientsByGroupAsync(string groupName, int userId, PaginationParams paginationParams);
        Task<bool> DeleteRecipientAsync(int id);
        Task<IEnumerable<RecipientResponseDto>> ImportRecipientsAsync(List<CreateRecipientDto> dtos, int userId);
    }
}
