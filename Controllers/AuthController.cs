using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;
using System.Security.Claims;

namespace FeedBackGeneratorApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // ─────────────────────────────────────────────────
        // POST /api/auth/register
        // ─────────────────────────────────────────────────
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
        {
            var result = await _authService.RegisterAsync(dto);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────
        // POST /api/auth/login
        // ─────────────────────────────────────────────────
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────
        // POST /api/auth/refresh
        // ─────────────────────────────────────────────────
        /// <summary>
        /// Exchange a valid refresh token for a new access token + rotated refresh token.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshTokenRequestDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────
        // POST /api/auth/revoke
        // ─────────────────────────────────────────────────
        /// <summary>
        /// Revoke a refresh token (logout). Requires a valid access token.
        /// </summary>
        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> Revoke([FromBody] RefreshTokenRequestDto dto)
        {
            await _authService.RevokeTokenAsync(dto.RefreshToken);
            return NoContent();
        }

        // ─────────────────────────────────────────────────
        // GET /api/auth/users  [Admin only]
        // ─────────────────────────────────────────────────
        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PagedResult<UserResponseDto>>> GetAllUsers([FromQuery] PaginationParams paginationParams)
        {
            var users = await _authService.GetAllUsersAsync(paginationParams);
            return Ok(users);
        }

        // ─────────────────────────────────────────────────
        // GET /api/auth/users/{id}  [Admin only]
        // ─────────────────────────────────────────────────
        [HttpGet("users/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDto>> GetUserById(int id)
        {
            var user = await _authService.GetUserByIdAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }
    }
}
