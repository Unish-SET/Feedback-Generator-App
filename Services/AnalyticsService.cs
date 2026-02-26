using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly FeedbackDbContext _context;

        public AnalyticsService(FeedbackDbContext context)
        {
            _context = context;
        }

        public async Task<SurveyAnalyticsDto?> GetSurveyAnalyticsAsync(int surveyId)
        {
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == surveyId);

            if (survey == null) return null;

            var responses = await _context.SurveyResponses
                .Where(r => r.SurveyId == surveyId)
                .ToListAsync();

            var totalResponses = responses.Count;
            var completedResponses = responses.Count(r => r.IsComplete);

            var analytics = new SurveyAnalyticsDto
            {
                SurveyId = surveyId,
                SurveyTitle = survey.Title,
                TotalResponses = totalResponses,
                CompletedResponses = completedResponses,
                IncompleteResponses = totalResponses - completedResponses,
                CompletionRate = totalResponses > 0 ? Math.Round((double)completedResponses / totalResponses * 100, 2) : 0,
                QuestionAnalytics = new List<QuestionAnalyticsDto>()
            };

            foreach (var question in survey.Questions)
            {
                var answers = await _context.Answers
                    .Where(a => a.QuestionId == question.Id)
                    .ToListAsync();

                var questionAnalytics = new QuestionAnalyticsDto
                {
                    QuestionId = question.Id,
                    QuestionText = question.Text,
                    QuestionType = question.QuestionType,
                    TotalAnswers = answers.Count
                };

                switch (question.QuestionType)
                {
                    case "MultipleChoice":
                    case "YesNo":
                        questionAnalytics.AnswerDistribution = answers
                            .GroupBy(a => a.AnswerText)
                            .ToDictionary(g => g.Key, g => g.Count());
                        break;

                    case "Rating":
                        questionAnalytics.AnswerDistribution = answers
                            .GroupBy(a => a.AnswerText)
                            .ToDictionary(g => g.Key, g => g.Count());
                        var numericAnswers = answers
                            .Select(a => double.TryParse(a.AnswerText, out var val) ? val : (double?)null)
                            .Where(v => v.HasValue)
                            .Select(v => v!.Value)
                            .ToList();
                        questionAnalytics.AverageRating = numericAnswers.Any() ? Math.Round(numericAnswers.Average(), 2) : null;
                        break;

                    case "OpenText":
                        questionAnalytics.OpenTextResponses = answers.Select(a => a.AnswerText).ToList();
                        break;
                }

                analytics.QuestionAnalytics.Add(questionAnalytics);
            }

            return analytics;
        }
    }
}
