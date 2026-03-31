using FeedBackApp.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackApp.Models
{
    /// <summary>
    /// A reusable question stored in the question bank.
    /// Not tied to any survey or version — acts as a template.
    /// </summary>
    public class BankQuestion
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Owner of this bank question.</summary>
        [Required]
        public int CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public User Creator { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Text { get; set; } = string.Empty;

        [Required]
        public QuestionType Type { get; set; }

        public bool IsRequired { get; set; } = false;

        /// <summary>Optional tag/category for filtering (e.g. "NPS", "Satisfaction").</summary>
        [MaxLength(100)]
        public string? Tag { get; set; }

        /// <summary>SHA256 hash of normalized (Text + Type + Options) for deduplication.</summary>
        [MaxLength(64)]
        public string? Hash { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<BankQuestionOption> Options { get; set; } = new List<BankQuestionOption>();
    }
}
