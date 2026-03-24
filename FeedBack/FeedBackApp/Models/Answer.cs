using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models
{
    public class Answer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ResponseId { get; set; }

        [ForeignKey("ResponseId")]
        public SurveyResponse Response { get; set; } = null!;

        [Required]
        public int QuestionId { get; set; }

        [ForeignKey("QuestionId")]
        public Question Question { get; set; } = null!;

        public int? SelectedOptionId { get; set; }

        [ForeignKey("SelectedOptionId")]
        public QuestionOption? SelectedOption { get; set; }

        [MaxLength(5000)]
        public string? TextValue { get; set; }

        public int? RatingValue { get; set; }

        // For multi-select: store comma-separated option IDs
        [MaxLength(1000)]
        public string? SelectedOptionIds { get; set; }
    }
}
