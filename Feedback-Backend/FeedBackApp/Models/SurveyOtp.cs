using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackApp.Models
{
    public class SurveyOtp
    {
        [Key] public int Id { get; set; }

        [Required] public int SurveyId { get; set; }
        [ForeignKey("SurveyId")] public Survey Survey { get; set; } = null!;

        [Required][MaxLength(200)] public string Email { get; set; } = string.Empty;

        [Required][MaxLength(6)] public string Code { get; set; } = string.Empty;

        [Required] public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
