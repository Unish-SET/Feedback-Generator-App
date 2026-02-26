using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackGeneratorApp.Models
{
    public class Survey
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public int CreatedByUserId { get; set; }

        public bool IsActive { get; set; } = true;

        public int Version { get; set; } = 1;

        public string? BrandingConfig { get; set; } // JSON string for branding customization

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("CreatedByUserId")]
        public User CreatedByUser { get; set; } = null!;

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<SurveyDistribution> SurveyDistributions { get; set; } = new List<SurveyDistribution>();
        public ICollection<SurveyResponse> SurveyResponses { get; set; } = new List<SurveyResponse>();
    }
}
