using CsvHelper;
using CsvHelper.Configuration;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace FeedBackApp.Services
{
    public class ExportService : IExportService
    {
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IRepository<SurveyVersion> _versionRepo;
        private readonly IRepository<SurveyResponse> _responseRepo;

        public ExportService(
            IRepository<Survey> surveyRepo,
            IRepository<SurveyVersion> versionRepo,
            IRepository<SurveyResponse> responseRepo)
        {
            _surveyRepo = surveyRepo;
            _versionRepo = versionRepo;
            _responseRepo = responseRepo;
        }

        public async Task<byte[]> ExportCsvAsync(int surveyId, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (role != UserRole.Admin.ToString() && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to export this survey's data.");

            var latestVersion = await _versionRepo.GetQueryable()
                .Include(v => v.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.Options)
                .Where(v => v.SurveyId == surveyId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            if (latestVersion == null)
                throw new BadRequestException("Survey has no version.");

            var responses = await _responseRepo.GetQueryable()
                .Include(r => r.User)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.SelectedOption)
                .Where(r => r.SurveyVersionId == latestVersion.Id)
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync();

            var questions = latestVersion.Questions.OrderBy(q => q.Order).ToList();

            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

            // Header
            csv.WriteField("Survey Title");
            csv.WriteField("Version");
            csv.WriteField("ResponseId");
            csv.WriteField("Respondent");
            csv.WriteField("SubmittedAt");

            foreach (var question in questions)
            {
                csv.WriteField(question.Text);
            }
            csv.NextRecord();

            // Data rows
            foreach (var response in responses)
            {
                csv.WriteField(survey.Title);
                csv.WriteField(latestVersion.VersionNumber);
                csv.WriteField(response.Id);
                csv.WriteField(response.User?.Username ?? "Anonymous");
                csv.WriteField(response.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                foreach (var question in questions)
                {
                    var answer = response.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
                    if (answer == null)
                    {
                        csv.WriteField("");
                        continue;
                    }

                    switch (question.Type)
                    {
                        case QuestionType.SingleChoice:
                            csv.WriteField(answer.SelectedOption?.Text ?? "");
                            break;
                        case QuestionType.MultipleChoice:
                            if (!string.IsNullOrEmpty(answer.SelectedOptionIds))
                            {
                                var optionIds = answer.SelectedOptionIds.Split(',').Select(int.Parse).ToList();
                                var optionTexts = question.Options
                                    .Where(o => optionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                csv.WriteField(string.Join("; ", optionTexts));
                            }
                            else
                            {
                                csv.WriteField("");
                            }
                            break;
                        case QuestionType.RatingScale:
                            csv.WriteField(answer.RatingValue?.ToString() ?? "");
                            break;
                        case QuestionType.ShortText:
                        case QuestionType.LongText:
                            csv.WriteField(answer.TextValue ?? "");
                            break;
                        default:
                            csv.WriteField("");
                            break;
                    }
                }
                csv.NextRecord();
            }

            await writer.FlushAsync();
            return memoryStream.ToArray();
        }
    }
}
