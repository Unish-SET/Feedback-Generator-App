using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models
{
    public class SurveyResponse
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SurveyId { get; set; }

        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;

        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        /// <summary>Stable browser UUID sent by anonymous respondents to prevent duplicate submissions.</summary>
        [MaxLength(64)]
        public string? AnonToken { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
