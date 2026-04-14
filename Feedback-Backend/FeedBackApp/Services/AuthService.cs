using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FeedBackApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IConfiguration    _configuration;
        private readonly IAuditService     _audit;

        public AuthService(IRepository<User> userRepo, IConfiguration configuration, IAuditService audit)
        {
            _userRepo      = userRepo;
            _configuration = configuration;
            _audit         = audit;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            if (await _userRepo.AnyAsync(u => u.Username == dto.Username))
                throw new ConflictException("Username already exists.");

            if (await _userRepo.AnyAsync(u => u.Email == dto.Email))
                throw new ConflictException("Email already exists.");


            using var hmac = new HMACSHA512();
            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password)),
                PasswordSalt = hmac.Key,
                Role = UserRole.Creator,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepo.AddAsync(user);
            await _userRepo.SaveChangesAsync();

            var token = GenerateToken(user);
            _ = _audit.LogAsync("Register", "User", user.Id.ToString(), user.Id);
            return new AuthResponseDto { Token = token };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _userRepo.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null)
                throw new BadRequestException("Invalid username or password.");

            using var hmac = new HMACSHA512(user.PasswordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password));

            if (!computedHash.SequenceEqual(user.PasswordHash))
                throw new BadRequestException("Invalid username or password.");

            
            if (user.IsDeleted || !user.IsActive)
                throw new BadRequestException("Invalid username or password.");

            var token = GenerateToken(user);
            _ = _audit.LogAsync("Login", "User", user.Id.ToString(), user.Id);
            return new AuthResponseDto { Token = token };
        }

        private string GenerateToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var jwtKey = jwtSettings["Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
                throw new InvalidOperationException("JwtSettings:Key is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expiryMinutesRaw = jwtSettings["ExpiryMinutes"];
            var expiryMinutes = double.TryParse(expiryMinutesRaw, out var parsed) ? parsed : 60;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
