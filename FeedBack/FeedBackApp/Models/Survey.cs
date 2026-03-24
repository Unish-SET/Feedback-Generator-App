using FeedBackApp.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models
{
    public class Survey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public Guid PublicToken { get; set; } = Guid.NewGuid();

        [Required]
        public SurveyStatus Status { get; set; } = SurveyStatus.Draft;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool AllowAnonymous { get; set; } = false;

        [Required]
        public int CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public User Creator { get; set; } = null!;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<SurveyVersion> Versions { get; set; } = new List<SurveyVersion>();
    }
}
