using AutoMapper;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Helpers;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackGeneratorApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IMapper _mapper;
        private readonly JwtHelper _jwtHelper;
        private readonly FeedbackDbContext _db;

        public AuthService(
            IRepository<User> userRepo,
            IMapper mapper,
            JwtHelper jwtHelper,
            FeedbackDbContext db)
        {
            _userRepo = userRepo;
            _mapper = mapper;
            _jwtHelper = jwtHelper;
            _db = db;
        }

        // ─────────────────────────────────────────────────
        // Register
        // ─────────────────────────────────────────────────
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            var existingUsers = await _userRepo.FindAsync(u => u.Email == dto.Email);
            if (existingUsers.Any())
                throw new ConflictException("A user with this email already exists.");

            var user = _mapper.Map<User>(dto);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            user.CreatedAt = DateTime.UtcNow;

            await _userRepo.AddAsync(user);

            return await IssueTokensForUser(user);
        }

        // ─────────────────────────────────────────────────
        // Login
        // ─────────────────────────────────────────────────
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var users = await _userRepo.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedException("Invalid email or password.");

            return await IssueTokensForUser(user);
        }

        // ─────────────────────────────────────────────────
        // Refresh token (rotate)
        // ─────────────────────────────────────────────────
        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var storedToken = await _db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedToken == null)
                throw new UnauthorizedException("Invalid refresh token.");

            if (storedToken.IsRevoked)
                throw new UnauthorizedException("Refresh token has been revoked.");

            if (storedToken.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedException("Refresh token has expired.");

            // Revoke the old token (single-use rotation)
            storedToken.IsRevoked = true;
            _db.RefreshTokens.Update(storedToken);

            // Issue brand new access + refresh token pair
            return await IssueTokensForUser(storedToken.User);
        }

        // ─────────────────────────────────────────────────
        // Revoke token (logout)
        // ─────────────────────────────────────────────────
        public async Task RevokeTokenAsync(string refreshToken)
        {
            var storedToken = await _db.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedToken == null)
                throw new NotFoundException("Refresh token not found.");

            if (storedToken.IsRevoked)
                return; // Already revoked — idempotent

            storedToken.IsRevoked = true;
            _db.RefreshTokens.Update(storedToken);
            await _db.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────
        // User queries
        // ─────────────────────────────────────────────────
        public async Task<UserResponseDto?> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            return user == null ? null : _mapper.Map<UserResponseDto>(user);
        }

        public async Task<PagedResult<UserResponseDto>> GetAllUsersAsync(PaginationParams paginationParams)
        {
            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(search)
                    || u.Email.ToLower().Contains(search)
                    || u.Role.ToLower().Contains(search));
            }

            query = paginationParams.SortBy?.ToLower() switch
            {
                "name"  => paginationParams.SortDescending ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                "email" => paginationParams.SortDescending ? query.OrderByDescending(u => u.Email)    : query.OrderBy(u => u.Email),
                "role"  => paginationParams.SortDescending ? query.OrderByDescending(u => u.Role)     : query.OrderBy(u => u.Role),
                _       => query.OrderByDescending(u => u.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var users = await query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToListAsync();

            return new PagedResult<UserResponseDto>
            {
                Items      = _mapper.Map<List<UserResponseDto>>(users),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize   = paginationParams.PageSize
            };
        }

        // ─────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────
        private async Task<AuthResponseDto> IssueTokensForUser(User user)
        {
            var accessToken    = _jwtHelper.GenerateAccessToken(user.Id, user.Email, user.Role);
            var rawRefresh     = _jwtHelper.GenerateRefreshToken();
            var tokenExpiresAt = _jwtHelper.AccessTokenExpiresAt;

            var refreshTokenEntity = new RefreshToken
            {
                Token     = rawRefresh,
                UserId    = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtHelper.RefreshTokenExpiryDays),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            await _db.RefreshTokens.AddAsync(refreshTokenEntity);
            await _db.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token        = accessToken,
                RefreshToken = rawRefresh,
                TokenExpiresAt = tokenExpiresAt,
                User         = _mapper.Map<UserResponseDto>(user)
            };
        }
    }
}
