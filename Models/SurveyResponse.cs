using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackGeneratorApp.Models
{
    public class SurveyResponse
    {
        public int Id { get; set; }

        public int SurveyId { get; set; }

        public int? RespondentUserId { get; set; } // Nullable for anonymous responses

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public bool IsComplete { get; set; } = false;

        // Navigation Properties
        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;

        [ForeignKey("RespondentUserId")]
        public User? RespondentUser { get; set; }

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
