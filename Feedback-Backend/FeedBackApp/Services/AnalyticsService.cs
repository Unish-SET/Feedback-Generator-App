using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models;
using FeedBackApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Text;

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

            var responseQuery = _responseRepo.GetQueryable()
                .Where(r => r.SurveyId == surveyId);

            if (filter?.FromDate.HasValue == true)
                responseQuery = responseQuery.Where(r => r.SubmittedAt >= filter.FromDate.Value.ToUniversalTime());

            if (filter?.ToDate.HasValue == true)
                responseQuery = responseQuery.Where(r => r.SubmittedAt <= filter.ToDate.Value.ToUniversalTime());

   
            var submittedDates = await responseQuery
                .Select(r => r.SubmittedAt)
                .ToListAsync();

            var totalResponses = submittedDates.Count;

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
                        var countsByOption = question.Answers
                            .Where(a => a.SelectedOptionId.HasValue)
                            .GroupBy(a => a.SelectedOptionId!.Value)
                            .ToDictionary(g => g.Key, g => g.Count());
                        var totalSingle = countsByOption.Values.Sum();
                        qa.OptionDistributions = question.Options
                            .OrderBy(o => o.Order)
                            .Select(o =>
                            {
                                var count = countsByOption.GetValueOrDefault(o.Id, 0);
                                return new OptionDistributionDto
                                {
                                    OptionId   = o.Id,
                                    OptionText = o.Text,
                                    Count      = count,
                                    Percentage = totalSingle > 0
                                        ? Math.Round((double)count / totalSingle * 100, 2)
                                        : 0
                                };
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

        public async Task<(string Html, string Title)> BuildReportHtmlAsync(int surveyId, int userId, string role)
        {
            var data = await GetAnalyticsAsync(surveyId, userId, role);
            var sb   = new StringBuilder();

            sb.Append("<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:32px;color:#1e293b;background:#ffffff'>");

            // Header
            sb.Append("<div style='border-bottom:2px solid #6366f1;padding-bottom:16px;margin-bottom:24px'>");
            sb.AppendFormat("<h1 style='font-size:22px;margin:0;color:#1e293b'>{0}</h1>", Encode(data.SurveyTitle));
            sb.Append("<p style='color:#64748b;margin:4px 0 0;font-size:13px'>Analytics Report</p>");
            sb.Append("</div>");

            // Total responses
            sb.Append("<div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:20px;text-align:center;margin-bottom:28px'>");
            sb.AppendFormat("<div style='font-size:48px;font-weight:700;color:#6366f1'>{0}</div>", data.TotalResponses);
            sb.Append("<div style='color:#64748b;font-size:14px;margin-top:4px'>Total Responses</div>");
            sb.Append("</div>");

            // Daily trend table
            if (data.DateWiseCounts.Any())
            {
                sb.Append("<h2 style='font-size:15px;margin:0 0 12px'>Response Timeline</h2>");
                sb.Append("<table style='width:100%;border-collapse:collapse;font-size:13px;margin-bottom:28px'>");
                sb.Append("<tr style='background:#f1f5f9'>");
                sb.Append("<th style='text-align:left;padding:8px 12px;border:1px solid #e2e8f0'>Date</th>");
                sb.Append("<th style='text-align:right;padding:8px 12px;border:1px solid #e2e8f0'>Responses</th>");
                sb.Append("</tr>");
                foreach (var d in data.DateWiseCounts)
                {
                    sb.Append("<tr>");
                    sb.AppendFormat("<td style='padding:8px 12px;border:1px solid #e2e8f0'>{0}</td>", Encode(d.Date));
                    sb.AppendFormat("<td style='padding:8px 12px;border:1px solid #e2e8f0;text-align:right'>{0}</td>", d.Count);
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }

            // Per-question breakdown
            sb.Append("<h2 style='font-size:15px;margin:0 0 16px'>Question Breakdown</h2>");
            foreach (var q in data.Questions)
            {
                sb.Append("<div style='border:1px solid #e2e8f0;border-radius:8px;padding:20px;margin-bottom:16px'>");
                sb.AppendFormat("<p style='font-weight:600;font-size:14px;margin:0 0 4px;color:#1e293b'>{0}</p>", Encode(q.QuestionText));
                sb.AppendFormat("<p style='font-size:11px;color:#94a3b8;margin:0 0 14px;text-transform:uppercase'>{0}</p>", Encode(q.QuestionType));

                // Rating
                if (q.AverageRating.HasValue)
                {
                    sb.AppendFormat(
                        "<div style='background:#faf5ff;border-radius:6px;padding:12px;text-align:center'>" +
                        "<span style='font-size:28px;font-weight:700;color:#6366f1'>{0:F1}</span>" +
                        "<span style='color:#94a3b8;font-size:13px'> / 5 average rating</span>" +
                        "</div>", q.AverageRating.Value);
                }

                // Option distributions — ASCII bar, no inline style width %
                if (q.OptionDistributions.Any())
                {
                    sb.Append("<table style='width:100%;border-collapse:collapse;font-size:13px'>");
                    foreach (var opt in q.OptionDistributions)
                    {
                        var pct = (int)Math.Round(opt.Percentage);
                        var bar = new string('█', pct / 5) + new string('░', 20 - pct / 5);
                        sb.Append("<tr>");
                        sb.AppendFormat("<td style='padding:5px 8px 5px 0;width:40%;color:#374151'>{0}</td>", Encode(opt.OptionText));
                        sb.AppendFormat("<td style='padding:5px 4px;font-family:monospace;color:#6366f1;font-size:11px'>{0}</td>", bar);
                        sb.AppendFormat("<td style='padding:5px 0 5px 8px;text-align:right;color:#64748b;white-space:nowrap'>{0}% ({1})</td>", pct, opt.Count);
                        sb.Append("</tr>");
                    }
                    sb.Append("</table>");
                }

                // Text responses
                if (q.TextResponses.Any())
                {
                    sb.Append("<div style='margin-top:10px'>");
                    sb.AppendFormat("<p style='font-size:12px;color:#94a3b8;margin:0 0 8px'>Sample responses ({0} total)</p>", q.TextResponses.Count);
                    foreach (var text in q.TextResponses.Take(5))
                    {
                        sb.AppendFormat(
                            "<div style='background:#f8fafc;border-left:3px solid #6366f1;padding:8px 12px;margin:4px 0;font-size:13px;color:#374151'>{0}</div>",
                            Encode(text));
                    }
                    sb.Append("</div>");
                }

                sb.Append("</div>");
            }

            // Footer
            sb.Append("<p style='text-align:center;color:#94a3b8;font-size:12px;margin-top:24px;border-top:1px solid #e2e8f0;padding-top:16px'>Generated by FeedBack App</p>");
            sb.Append("</div>");

            return (sb.ToString(), data.SurveyTitle);
        }

        private static string Encode(string input) =>
            System.Net.WebUtility.HtmlEncode(input ?? string.Empty);
    }
}