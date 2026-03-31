using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBackApp.Models
{
    public class BankQuestionOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BankQuestionId { get; set; }

        [ForeignKey("BankQuestionId")]
        public BankQuestion BankQuestion { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        public int Order { get; set; }
    }
}
