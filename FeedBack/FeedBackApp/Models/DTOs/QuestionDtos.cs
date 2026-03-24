using System.ComponentModel.DataAnnotations;

namespace FeedBackApp.Models.DTOs
{
    public class CreateQuestionDto
    {
        [Required]
        [MaxLength(1000)]
        public string Text { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public bool IsRequired { get; set; } = false;
        public int Order { get; set; }
        public List<CreateOptionDto> Options { get; set; } = new();
    }

    public class UpdateQuestionDto
    {
        [Required]
        [MaxLength(1000)]
        public string Text { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public bool IsRequired { get; set; } = false;
        public int Order { get; set; }
        public List<CreateOptionDto> Options { get; set; } = new();
    }

    public class CreateOptionDto
    {
        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        public int Order { get; set; }
    }

    public class QuestionResponseDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public int Order { get; set; }
        public List<OptionResponseDto> Options { get; set; } = new();
    }

    public class OptionResponseDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
