using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models
{
    public class SurveyVersion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SurveyId { get; set; }
        
        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;

        [Required]
        public int VersionNumber { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<SurveyResponse> Responses { get; set; } = new List<SurveyResponse>();
    }
}
