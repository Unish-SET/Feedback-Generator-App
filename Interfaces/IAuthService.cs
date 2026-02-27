using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<UserResponseDto?> GetUserByIdAsync(int id);
        Task<PagedResult<UserResponseDto>> GetAllUsersAsync(PaginationParams paginationParams);

        /// <summary>Validates the refresh token and issues a new access + refresh token pair (rotation).</summary>
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);

        /// <summary>Revokes a refresh token so it can no longer be used.</summary>
        Task RevokeTokenAsync(string refreshToken);
    }
}
