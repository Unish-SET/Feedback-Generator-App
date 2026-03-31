using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models.DTOs
{
    public class RegisterDto
    {
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;

    }

    public class LoginDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token    { get; set; } = string.Empty;

        // ALIGN-03: added so the Angular client can read user identity directly from the
        // login/register response instead of manually decoding JWT claim URNs — which breaks
        // silently if .NET ever changes its ClaimTypes URI format.
        public int    UserId   { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email    { get; set; } = string.Empty;
        public string Role     { get; set; } = string.Empty;
    }
}
