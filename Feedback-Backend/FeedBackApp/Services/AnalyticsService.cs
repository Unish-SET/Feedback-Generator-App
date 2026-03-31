using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models;
using FeedBackApp.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IRepository<Survey>         _surveyRepo;
        private readonly IRepository<SurveyResponse> _responseRepo;
        private readonly IRepository<Question>       _questionRepo;

        public AnalyticsService(
            IRepository<Survey>         surveyRepo,
            IRepository<SurveyResponse> responseRepo,
            IRepository<Question>       questionRepo)
        {
            _surveyRepo   = surveyRepo;
            _responseRepo = responseRepo;
            _questionRepo = questionRepo;
        }

        public async Task<SurveyAnalyticsDto> GetAnalyticsAsync(int surveyId, int userId, string role, AnalyticsFilterParams? filter = null)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey's analytics.");

            // ── Response query with optional date range ────────────────────────
            var responseQuery = _responseRepo.GetQueryable()
                .Where(r => r.SurveyId == surveyId);

            if (filter?.FromDate.HasValue == true)
                responseQuery = responseQuery.Where(r => r.SubmittedAt >= filter.FromDate.Value.ToUniversalTime());

            if (filter?.ToDate.HasValue == true)
                responseQuery = responseQuery.Where(r => r.SubmittedAt <= filter.ToDate.Value.ToUniversalTime());

            // BUG-09 FIX: original code ran responseQuery twice — once for CountAsync and once
            // for Select(r => r.SubmittedAt).ToListAsync() — two full table scans.
            // Single query: fetch all SubmittedAt values; derive both count and date grouping
            // from the in-memory list. For large surveys this halves the DB round-trips here.
            var submittedDates = await responseQuery
                .Select(r => r.SubmittedAt)
                .ToListAsync();

            var totalResponses = submittedDates.Count;

            // ── Questions ─────────────────────────────────────────────────────
            var questions = await _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .Include(q => q.Answers)
                    .ThenInclude(a => a.SelectedOption)
                .Where(q => q.SurveyId == surveyId)
                .OrderBy(q => q.Order)
                .ToListAsync();

            var questionAnalytics = new List<QuestionAnalyticsDto>();

            foreach (var question in questions)
            {
                var qa = new QuestionAnalyticsDto
                {
                    QuestionId   = question.Id,
                    QuestionText = question.Text,
                    QuestionType = question.Type.ToString()
                };

                switch (question.Type)
                {
                    case Models.Enums.QuestionType.RatingScale:
                        var ratings = question.Answers
                            .Where(a => a.RatingValue.HasValue)
                            .Select(a => a.RatingValue!.Value)
                            .ToList();
                        qa.AverageRating = ratings.Any() ? ratings.Average() : null;
                        break;

                    case Models.Enums.QuestionType.SingleChoice:
                        var totalSingle = question.Answers.Count(a => a.SelectedOptionId.HasValue);
                        qa.OptionDistributions = question.Options
                            .OrderBy(o => o.Order)
                            .Select(o => new OptionDistributionDto
                            {
                                OptionId   = o.Id,
                                OptionText = o.Text,
                                Count      = question.Answers.Count(a => a.SelectedOptionId == o.Id),
                                Percentage = totalSingle > 0
                                    ? Math.Round((double)question.Answers.Count(a => a.SelectedOptionId == o.Id) / totalSingle * 100, 2)
                                    : 0
                            }).ToList();
                        break;

                    case Models.Enums.QuestionType.MultipleChoice:
                        var allSelectedIds = question.Answers
                            .Where(a => !string.IsNullOrEmpty(a.SelectedOptionIds))
                            .SelectMany(a => a.SelectedOptionIds!
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => int.TryParse(x.Trim(), out var pid) ? pid : (int?)null)
                                .Where(x => x.HasValue)
                                .Select(x => x!.Value))
                            .ToList();
                        var totalMulti = allSelectedIds.Count;
                        qa.OptionDistributions = question.Options
                            .OrderBy(o => o.Order)
                            .Select(o => new OptionDistributionDto
                            {
                                OptionId   = o.Id,
                                OptionText = o.Text,
                                Count      = allSelectedIds.Count(id => id == o.Id),
                                Percentage = totalMulti > 0
                                    ? Math.Round((double)allSelectedIds.Count(id => id == o.Id) / totalMulti * 100, 2)
                                    : 0
                            }).ToList();
                        break;

                    case Models.Enums.QuestionType.ShortText:
                    case Models.Enums.QuestionType.LongText:
                        qa.TextResponses = question.Answers
                            .Where(a => !string.IsNullOrEmpty(a.TextValue))
                            .Select(a => a.TextValue!)
                            .ToList();
                        break;
                }

                questionAnalytics.Add(qa);
            }

            // ── Date-wise counts ──────────────────────────────────────────────
            // submittedDates already fetched above — no second DB query needed
            var dateWiseCounts = submittedDates
                .GroupBy(d => d.Date)
                .Select(g => new DateWiseCountDto
                {
                    Date  = g.Key.ToString("yyyy-MM-dd"),
                    Count = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            return new SurveyAnalyticsDto
            {
                SurveyId       = surveyId,
                SurveyTitle    = survey.Title,
                TotalResponses = totalResponses,
                Questions      = questionAnalytics,
                DateWiseCounts = dateWiseCounts
            };
        }
    }
}