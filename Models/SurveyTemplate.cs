using System.ComponentModel.DataAnnotations;

namespace FeedBackGeneratorApp.Models
{
    public class SurveyTemplate
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public string TemplateData { get; set; } = string.Empty; // JSON containing survey structure

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
