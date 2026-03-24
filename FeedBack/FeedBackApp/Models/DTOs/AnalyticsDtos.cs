namespace FeedBackApp.Models.DTOs
{
    public class SurveyAnalyticsDto
    {
        public int SurveyId { get; set; }
        public string SurveyTitle { get; set; } = string.Empty;
        public int TotalResponses { get; set; }
        public List<QuestionAnalyticsDto> Questions { get; set; } = new();
        public List<DateWiseCountDto> DateWiseCounts { get; set; } = new();
    }

    public class QuestionAnalyticsDto
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public double? AverageRating { get; set; }
        public List<OptionDistributionDto> OptionDistributions { get; set; } = new();
        public List<string> TextResponses { get; set; } = new();
    }

    public class OptionDistributionDto
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class DateWiseCountDto
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
