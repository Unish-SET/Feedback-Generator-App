using System.ComponentModel.DataAnnotations;

namespace FeedBackGeneratorApp.DTOs
{
    public class CreateRecipientDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? GroupName { get; set; }
    }

    public class RecipientResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? GroupName { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
