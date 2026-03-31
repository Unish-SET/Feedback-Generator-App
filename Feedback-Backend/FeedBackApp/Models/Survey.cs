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

        /// <summary>
        /// Single source of truth for survey lifecycle.
        /// Inactive = editable, Active = live (accepting responses), Closed = permanently locked.
        /// </summary>
        [Required]
        public SurveyState State { get; set; } = SurveyState.Inactive;

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

        public ICollection<Question>       Questions  { get; set; } = new List<Question>();
        public ICollection<SurveyResponse> Responses  { get; set; } = new List<SurveyResponse>();
    }
}
