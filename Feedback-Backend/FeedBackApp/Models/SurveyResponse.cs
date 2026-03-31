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

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
