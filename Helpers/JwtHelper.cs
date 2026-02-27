using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FeedBackGeneratorApp.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _configuration;

        public JwtHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ──────────────────────────────────────────────────
        // Access token generation
        // ──────────────────────────────────────────────────
        public string GenerateAccessToken(int userId, string email, string role)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expiryMinutes = double.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "15");

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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

        /// <summary>Kept for backward-compat; calls GenerateAccessToken internally.</summary>
        public string GenerateToken(int userId, string email, string role)
            => GenerateAccessToken(userId, email, role);

        // ──────────────────────────────────────────────────
        // Refresh token generation
        // ──────────────────────────────────────────────────
        public string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        public int RefreshTokenExpiryDays
        {
            get
            {
                var days = _configuration["JwtSettings:RefreshTokenExpiryDays"];
                return int.TryParse(days, out var d) ? d : 7;
            }
        }

        public DateTime AccessTokenExpiresAt
        {
            get
            {
                var minutes = _configuration["JwtSettings:AccessTokenExpiryMinutes"];
                return double.TryParse(minutes, out var m)
                    ? DateTime.UtcNow.AddMinutes(m)
                    : DateTime.UtcNow.AddMinutes(15);
            }
        }

        // ──────────────────────────────────────────────────
        // Validate an (optionally expired) token and return its principal
        // ──────────────────────────────────────────────────
        public ClaimsPrincipal? ValidateTokenAndGetPrincipal(string token)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                // Allow expired tokens so we can extract claims during refresh
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, validationParams, out var validatedToken);

                // Ensure the algorithm is correct (guard against "alg:none" attacks)
                if (validatedToken is not JwtSecurityToken jwt ||
                    !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                    return null;

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
