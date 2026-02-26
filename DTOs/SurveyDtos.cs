using System.ComponentModel.DataAnnotations;

namespace FeedBackGeneratorApp.DTOs
{
    public class CreateSurveyDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public string? BrandingConfig { get; set; }

        public List<CreateQuestionDto>? Questions { get; set; }
    }

    public class UpdateSurveyDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public bool? IsActive { get; set; }

        public string? BrandingConfig { get; set; }
    }

    public class SurveyResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int Version { get; set; }
        public string? BrandingConfig { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ShareableLink { get; set; }
        public List<QuestionResponseDto> Questions { get; set; } = new();
    }

    public class CreateQuestionDto
    {
        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string QuestionType { get; set; } = "OpenText";

        public string? Options { get; set; }

        public bool IsRequired { get; set; } = false;

        public int OrderIndex { get; set; } = 0;
    }

    public class QuestionResponseDto
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string? Options { get; set; }
        public bool IsRequired { get; set; }
        public int OrderIndex { get; set; }
    }
}
