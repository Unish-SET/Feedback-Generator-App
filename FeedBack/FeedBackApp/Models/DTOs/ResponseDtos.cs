using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models.DTOs
{
    public class SubmitResponseDto
    {
        [Required]
        public int SurveyVersionId { get; set; }

        [Required]
        public List<SubmitAnswerDto> Answers { get; set; } = new();
    }

    public class SubmitAnswerDto
    {
        [Required]
        public int QuestionId { get; set; }

        public int? SelectedOptionId { get; set; }

        public string? TextValue { get; set; }

        public int? RatingValue { get; set; }

        // For multi-select questions
        public List<int>? SelectedOptionIds { get; set; }
    }

    public class ResponseListDto
    {
        public int Id { get; set; }
        public int SurveyVersionId { get; set; }
        public int VersionNumber { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public DateTime SubmittedAt { get; set; }
        public List<AnswerDto> Answers { get; set; } = new();
    }

    public class AnswerDto
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public int? SelectedOptionId { get; set; }
        public string? SelectedOptionText { get; set; }
        public string? TextValue { get; set; }
        public int? RatingValue { get; set; }
        public List<int>? SelectedOptionIds { get; set; }
    }
}
