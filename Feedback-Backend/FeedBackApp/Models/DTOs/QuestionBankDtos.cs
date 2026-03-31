using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models.DTOs
{
    // ── Create / Update ───────────────────────────────────────────────────────

    public class CreateBankQuestionDto
    {
        [Required]
        [MaxLength(1000)]
        public string Text { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public bool IsRequired { get; set; } = false;

        [MaxLength(100)]
        public string? Tag { get; set; }

        public List<CreateOptionDto> Options { get; set; } = new();
    }

    public class UpdateBankQuestionDto
    {
        [Required]
        [MaxLength(1000)]
        public string Text { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public bool IsRequired { get; set; } = false;

        [MaxLength(100)]
        public string? Tag { get; set; }

        public List<CreateOptionDto> Options { get; set; } = new();
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public class BankQuestionDto
    {
        public int              Id         { get; set; }
        public string           Text       { get; set; } = string.Empty;
        public string           Type       { get; set; } = string.Empty;
        public bool             IsRequired { get; set; }
        public string?          Tag        { get; set; }
        public DateTime         CreatedAt  { get; set; }
        public List<OptionResponseDto> Options { get; set; } = new();
    }

    // ── Clone from bank into a survey ─────────────────────────────────────────

    public class CloneFromBankDto
    {
        /// <summary>Bank question IDs to clone. Cloned in the order provided.</summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one bank question ID is required.")]
        public List<int> BankQuestionIds { get; set; } = new();

        /// <summary>Target survey ID (must be Draft).</summary>
        [Required]
        [Range(1, int.MaxValue)]
        public int TargetSurveyId { get; set; }
    }

    public class CloneFromBankResultDto
    {
        public List<int> NewQuestionIds { get; set; } = new();
        public int       ClonedCount    { get; set; }
        public string    Message        { get; set; } = string.Empty;
    }

    // ── Filter params ─────────────────────────────────────────────────────────

    public class BankQuestionFilterParams
    {
        public string? Search { get; set; }
        public string? Tag    { get; set; }
        public string? Type   { get; set; }
        public int     PageNumber { get; set; } = 1;
        public int     PageSize  { get; set; } = 20;
    }
}
