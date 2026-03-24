using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IUserService
    {
        Task<List<UserResponseDto>> GetAllUsersAsync();
        Task<UserResponseDto>       GetUserByIdAsync(int id);
        Task<UserResponseDto>       UpdateUserRoleAsync(int id, UpdateUserRoleDto dto);
        Task                        DeleteUserAsync(int id);
    }
}
