using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IRepository<SurveyVersion> _versionRepo;
        private readonly IRepository<SurveyResponse> _responseRepo;
        private readonly IRepository<Question> _questionRepo;

        public AnalyticsService(
            IRepository<Survey> surveyRepo,
            IRepository<SurveyVersion> versionRepo,
            IRepository<SurveyResponse> responseRepo,
            IRepository<Question> questionRepo)
        {
            _surveyRepo = surveyRepo;
            _versionRepo = versionRepo;
            _responseRepo = responseRepo;
            _questionRepo = questionRepo;
        }

        public async Task<SurveyAnalyticsDto> GetAnalyticsAsync(int surveyId, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (role != UserRole.Admin.ToString() && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey's analytics.");

            var latestVersion = await _versionRepo.GetQueryable()
                .Where(v => v.SurveyId == surveyId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            if (latestVersion == null)
                throw new BadRequestException("Survey has no version.");

            var allVersionIds = await _versionRepo.GetQueryable()
                .Where(v => v.SurveyId == surveyId)
                .Select(v => v.Id)
                .ToListAsync();

            var totalResponses = await _responseRepo.CountAsync(
                r => allVersionIds.Contains(r.SurveyVersionId));

            var questions = await _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .Include(q => q.Answers)
                    .ThenInclude(a => a.SelectedOption)
                .Where(q => q.SurveyVersionId == latestVersion.Id)
                .OrderBy(q => q.Order)
                .ToListAsync();

            var questionAnalytics = new List<QuestionAnalyticsDto>();

            foreach (var question in questions)
            {
                var qa = new QuestionAnalyticsDto
                {
                    QuestionId = question.Id,
                    QuestionText = question.Text,
                    QuestionType = question.Type.ToString()
                };

                switch (question.Type)
                {
                    case QuestionType.RatingScale:
                        var ratings = question.Answers
                            .Where(a => a.RatingValue.HasValue)
                            .Select(a => a.RatingValue!.Value)
                            .ToList();
                        qa.AverageRating = ratings.Any() ? ratings.Average() : null;
                        break;

                    case QuestionType.SingleChoice:
                        var totalSingle = question.Answers.Count(a => a.SelectedOptionId.HasValue);
                        qa.OptionDistributions = question.Options
                            .OrderBy(o => o.Order)
                            .Select(o => new OptionDistributionDto
                            {
                                OptionId = o.Id,
                                OptionText = o.Text,
                                Count = question.Answers.Count(a => a.SelectedOptionId == o.Id),
                                Percentage = totalSingle > 0
                                    ? Math.Round((double)question.Answers.Count(a => a.SelectedOptionId == o.Id) / totalSingle * 100, 2)
                                    : 0
                            }).ToList();
                        break;

                    case QuestionType.MultipleChoice:
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
                                OptionId = o.Id,
                                OptionText = o.Text,
                                Count = allSelectedIds.Count(id => id == o.Id),
                                Percentage = totalMulti > 0
                                    ? Math.Round((double)allSelectedIds.Count(id => id == o.Id) / totalMulti * 100, 2)
                                    : 0
                            }).ToList();
                        break;

                    case QuestionType.ShortText:
                    case QuestionType.LongText:
                        qa.TextResponses = question.Answers
                            .Where(a => !string.IsNullOrEmpty(a.TextValue))
                            .Select(a => a.TextValue!)
                            .ToList();
                        break;
                }

                questionAnalytics.Add(qa);
            }

            // ── FIX ──────────────────────────────────────────────────────────
            // SQL Server cannot translate .Date property inside GroupBy.
            // Pull SubmittedAt values to memory first, then group in C#.
            var submittedDates = await _responseRepo.GetQueryable()
                .Where(r => allVersionIds.Contains(r.SurveyVersionId))
                .Select(r => r.SubmittedAt)
                .ToListAsync();

            var dateWiseCounts = submittedDates
                .GroupBy(d => d.Date)
                .Select(g => new DateWiseCountDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Count = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();
            // ─────────────────────────────────────────────────────────────────

            return new SurveyAnalyticsDto
            {
                SurveyId = surveyId,
                SurveyTitle = survey.Title,
                TotalResponses = totalResponses,
                Questions = questionAnalytics,
                DateWiseCounts = dateWiseCounts
            };
        }
    }
}