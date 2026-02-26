using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackGeneratorApp.Models
{
    public class Question
    {
        public int Id { get; set; }

        public int SurveyId { get; set; }

        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        [MaxLength(30)]
        public string QuestionType { get; set; } = "OpenText"; // MultipleChoice, OpenText, Rating, YesNo

        public string? Options { get; set; } // JSON array for multiple choice options

        public bool IsRequired { get; set; } = false;

        public int OrderIndex { get; set; } = 0;

        // Navigation Properties
        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
