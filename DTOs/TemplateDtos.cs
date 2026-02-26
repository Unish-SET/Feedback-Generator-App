using System.ComponentModel.DataAnnotations;

namespace FeedBackGeneratorApp.DTOs
{
    public class CreateTemplateDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string TemplateData { get; set; } = string.Empty;
    }

    public class TemplateResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TemplateData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
