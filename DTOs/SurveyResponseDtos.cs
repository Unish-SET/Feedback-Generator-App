using System.ComponentModel.DataAnnotations;

namespace FeedBackGeneratorApp.DTOs
{
    public class SubmitResponseDto
    {
        [Required]
        public int SurveyId { get; set; }

        public List<SubmitAnswerDto> Answers { get; set; } = new();
    }

    public class SubmitAnswerDto
    {
        [Required]
        public int QuestionId { get; set; }

        [MaxLength(2000)]
        public string AnswerText { get; set; } = string.Empty;
    }

    public class SurveyResponseDetailDto
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }
        public string SurveyTitle { get; set; } = string.Empty;
        public int? RespondentUserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsComplete { get; set; }
        public List<AnswerResponseDto> Answers { get; set; } = new();
    }

    public class AnswerResponseDto
    {
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string AnswerText { get; set; } = string.Empty;
    }
}
