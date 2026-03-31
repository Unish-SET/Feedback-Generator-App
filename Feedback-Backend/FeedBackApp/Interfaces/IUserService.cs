using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IUserService
    {
        Task<PaginatedResult<UserResponseDto>> GetAllUsersAsync(UserFilterParams filter);
        Task<UserResponseDto>                  GetUserByIdAsync(int id);
        Task<UserResponseDto>                  UpdateUserRoleAsync(int id, UpdateUserRoleDto dto);
        Task<UserResponseDto>                  SetUserStatusAsync(int id, UpdateUserStatusDto dto);
        Task                                   SoftDeleteUserAsync(int id);
        Task<PaginatedResult<SurveyListDto>>   GetSurveysByUserAsync(int userId, int pageNumber, int pageSize);
    }
}
