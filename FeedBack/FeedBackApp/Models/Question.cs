using FeedBackApp.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models
{
    public class Question
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SurveyVersionId { get; set; }

        [ForeignKey("SurveyVersionId")]
        public SurveyVersion SurveyVersion { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Text { get; set; } = string.Empty;

        [Required]
        public QuestionType Type { get; set; }

        public bool IsRequired { get; set; } = false;

        public int Order { get; set; }

        // Navigation
        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
