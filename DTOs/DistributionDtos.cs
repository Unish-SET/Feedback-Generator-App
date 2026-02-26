using System.ComponentModel.DataAnnotations;

namespace FeedBackGeneratorApp.DTOs
{
    public class CreateDistributionDto
    {
        [Required]
        public int SurveyId { get; set; }

        [Required]
        [MaxLength(20)]
        public string DistributionType { get; set; } = "Link";

        [MaxLength(500)]
        public string? DistributionValue { get; set; }

        public DateTime? ScheduledAt { get; set; }
    }

    public class DistributionResponseDto
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }
        public string SurveyTitle { get; set; } = string.Empty;
        public string DistributionType { get; set; } = string.Empty;
        public string DistributionValue { get; set; } = string.Empty;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
