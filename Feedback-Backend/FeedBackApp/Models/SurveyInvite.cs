using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackApp.Models
{
    public class SurveyInvite
    {
        [Key] public int Id { get; set; }

        [Required] public int SurveyId { get; set; }
        [ForeignKey("SurveyId")] public Survey Survey { get; set; } = null!;

        [Required][MaxLength(200)] public string Email { get; set; } = string.Empty;

        [Required] public Guid InviteToken { get; set; } = Guid.NewGuid();

        public bool IsUsed { get; set; } = false;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAt { get; set; }
    }
}
