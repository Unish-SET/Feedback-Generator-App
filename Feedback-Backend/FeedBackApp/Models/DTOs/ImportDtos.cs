namespace FeedBackApp.Models.DTOs
{
    public class QuestionImportResultDto
    {
        public int          Total   { get; set; }
        public int          Success { get; set; }
        public int          Failed  { get; set; }
        public List<string> Errors  { get; set; } = new();
    }
}
