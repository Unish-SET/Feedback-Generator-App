namespace FeedBackGeneratorApp.DTOs
{
    public class SurveyAnalyticsDto
    {
        public int SurveyId { get; set; }
        public string SurveyTitle { get; set; } = string.Empty;
        public int TotalResponses { get; set; }
        public int CompletedResponses { get; set; }
        public int IncompleteResponses { get; set; }
        public double CompletionRate { get; set; }
        public List<QuestionAnalyticsDto> QuestionAnalytics { get; set; } = new();
    }

    public class QuestionAnalyticsDto
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public int TotalAnswers { get; set; }
        public Dictionary<string, int> AnswerDistribution { get; set; } = new();
        public double? AverageRating { get; set; }
        public List<string> OpenTextResponses { get; set; } = new();
    }
}
