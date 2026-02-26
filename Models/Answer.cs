using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackGeneratorApp.Models
{
    public class Answer
    {
        public int Id { get; set; }

        public int SurveyResponseId { get; set; }

        public int QuestionId { get; set; }

        [MaxLength(2000)]
        public string AnswerText { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("SurveyResponseId")]
        public SurveyResponse SurveyResponse { get; set; } = null!;

        [ForeignKey("QuestionId")]
        public Question Question { get; set; } = null!;
    }
}
