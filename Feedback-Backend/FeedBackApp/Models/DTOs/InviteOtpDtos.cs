using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models.DTOs
{
    // ── Invite ────────────────────────────────────────────────────────────────
    public class SendInvitesDto
    {
        [Required(ErrorMessage = "Email list is required.")]
        [MinLength(1, ErrorMessage = "At least one email is required.")]
        [MaxLength(100, ErrorMessage = "Cannot send more than 100 invites at once.")]
        public List<string> Emails { get; set; } = new();
    }

    public class SurveyInviteDto
    {
        public int      Id     { get; set; }
        public string   Email  { get; set; } = string.Empty;
        public bool     IsUsed { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class SetInviteOnlyDto
    {
        public bool IsInviteOnly { get; set; }
    }

    // ── OTP ───────────────────────────────────────────────────────────────────
    public class SendOtpDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Survey token is required.")]
        public Guid SurveyPublicToken { get; set; }
    }

    public class VerifyOtpDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Survey token is required.")]
        public Guid SurveyPublicToken { get; set; }

        [Required(ErrorMessage = "OTP code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "OTP must contain digits only.")]
        public string Code { get; set; } = string.Empty;
    }

    public class OtpVerifiedDto
    {
        public string   SessionToken { get; set; } = string.Empty;
        public DateTime ExpiresAt    { get; set; }
    }

    // ── Analytics report ──────────────────────────────────────────────────────
    public class SendAnalyticsReportDto
    {
        [Required(ErrorMessage = "Recipient email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(200, ErrorMessage = "Email must be under 200 characters.")]
        public string RecipientEmail { get; set; } = string.Empty;
    }
}
