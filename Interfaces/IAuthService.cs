using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<UserResponseDto?> GetUserByIdAsync(int id);
        Task<PagedResult<UserResponseDto>> GetAllUsersAsync(PaginationParams paginationParams);
    }
}
