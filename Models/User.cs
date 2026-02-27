using System.ComponentModel.DataAnnotations;

namespace FeedBackGeneratorApp.Models
{
    public class User
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Role { get; set; } = "Respondent"; // Admin, Staff, Viewer, Respondent

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public ICollection<Survey> Surveys { get; set; } = new List<Survey>();
        public ICollection<SurveyResponse> SurveyResponses { get; set; } = new List<SurveyResponse>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<Recipient> Recipients { get; set; } = new List<Recipient>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}
