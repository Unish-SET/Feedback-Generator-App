using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IResponseService
    {
        Task<ResponseListDto>                  SubmitAsync(Guid publicToken, SubmitResponseDto dto, int? userId, string? anonToken = null);
        Task<PaginatedResult<ResponseListDto>> GetResponsesAsync(int surveyId, ResponseFilterParams filter, int userId, string role);
    }
}
