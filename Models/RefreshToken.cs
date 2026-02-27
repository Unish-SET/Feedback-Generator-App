using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackGeneratorApp.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        [Required]
        public string Token { get; set; } = string.Empty;

        public int UserId { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRevoked { get; set; } = false;

        // Navigation
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
