using FeedBackApp.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models.DTOs
{
    public class CreateSurveyDto
    {
        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool AllowAnonymous { get; set; } = false;
    }

    public class UpdateSurveyDto
    {
        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool AllowAnonymous { get; set; } = false;
    }

    public class SurveyResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PublicToken { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool AllowAnonymous { get; set; }
        public int CreatedBy { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public int CurrentVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SurveyListDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PublicToken { get; set; } = string.Empty;
        public int ResponseCount { get; set; }
        public int CurrentVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatorName { get; set; } = string.Empty;
    }

    public class PublicSurveyDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool AllowAnonymous { get; set; }
        public int VersionId { get; set; }
        public List<PublicQuestionDto> Questions { get; set; } = new();
    }

    public class PublicQuestionDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public int Order { get; set; }
        public List<PublicOptionDto> Options { get; set; } = new();
    }

    public class PublicOptionDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
