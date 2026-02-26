using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackGeneratorApp.Models
{
    public class SurveyDistribution
    {
        public int Id { get; set; }

        public int SurveyId { get; set; }

        [MaxLength(20)]
        public string DistributionType { get; set; } = "Link"; // Email, Link, QRCode

        [MaxLength(500)]
        public string DistributionValue { get; set; } = string.Empty; // email address, link URL, QR data

        public DateTime? ScheduledAt { get; set; }

        public DateTime? SentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;
    }
}
