using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models.DTOs
{
    public class SubmitResponseDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one answer is required.")]
        public List<SubmitAnswerDto> Answers { get; set; } = new();
    }

    public class SubmitAnswerDto
    {
        [Required]
        public int QuestionId { get; set; }

        public int? SelectedOptionId { get; set; }

        public string? TextValue { get; set; }

        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int? RatingValue { get; set; }

        public List<int>? SelectedOptionIds { get; set; }
    }

    public class ResponseListDto
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }
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
