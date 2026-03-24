using ClosedXML.Excel;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class ExcelService : IExcelService
    {
        private readonly IRepository<Survey>         _surveyRepo;
        private readonly IRepository<SurveyVersion>  _versionRepo;
        private readonly IRepository<SurveyResponse> _responseRepo;
        private readonly ILogger<ExcelService>        _logger;

        public ExcelService(
            IRepository<Survey>         surveyRepo,
            IRepository<SurveyVersion>  versionRepo,
            IRepository<SurveyResponse> responseRepo,
            ILogger<ExcelService>        logger)
        {
            _surveyRepo   = surveyRepo;
            _versionRepo  = versionRepo;
            _responseRepo = responseRepo;
            _logger       = logger;
        }

        public async Task<byte[]> ExportExcelAsync(int surveyId, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId)
                ?? throw new NotFoundException($"Survey {surveyId} not found.");

            if (role != UserRole.Admin.ToString() && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to export this survey's data.");

            var latestVersion = await _versionRepo.GetQueryable()
                .Include(v => v.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.Options)
                .Where(v => v.SurveyId == surveyId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync()
                ?? throw new BadRequestException("Survey has no published version.");

            var responses = await _responseRepo.GetQueryable()
                .Include(r => r.User)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.SelectedOption)
                .Where(r => r.SurveyVersionId == latestVersion.Id)
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync();

            var questions = latestVersion.Questions.OrderBy(q => q.Order).ToList();

            _logger.LogInformation(
                "[EXCEL] Generating for Survey={SurveyId} Version={Version} Responses={Count} Questions={QCount}",
                surveyId, latestVersion.VersionNumber, responses.Count, questions.Count);

            using var wb = new XLWorkbook();
            BuildResponsesSheet(wb, survey, latestVersion, questions, responses);
            BuildSummarySheet(wb, survey, latestVersion, questions, responses);

            using var ms = new MemoryStream();

            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── Sheet 1: Responses ────────────────────────────────────────────────

        private static void BuildResponsesSheet(
            XLWorkbook           wb,
            Survey               survey,
            SurveyVersion        version,
            List<Question>       questions,
            List<SurveyResponse> responses)
        {
            var ws = wb.Worksheets.Add("Responses");

            var fixedHeaders = new[] { "ResponseId", "UserName", "SubmittedAt" };
            var allHeaders   = fixedHeaders.Concat(questions.Select(q => q.Text)).ToList();

            // Header row
            for (int col = 1; col <= allHeaders.Count; col++)
            {
                var cell = ws.Cell(1, col);
                cell.Value = allHeaders[col - 1];
                StyleHeaderCell(cell, isQuestion: col > fixedHeaders.Length);
            }

            // Data rows
            for (int row = 0; row < responses.Count; row++)
            {
                var r     = responses[row];
                int wsRow = row + 2;

                ws.Cell(wsRow, 1).Value = r.Id;
                ws.Cell(wsRow, 2).Value = r.User?.Username ?? "Anonymous";
                ws.Cell(wsRow, 3).Value = r.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss");

                for (int qi = 0; qi < questions.Count; qi++)
                {
                    var q      = questions[qi];
                    var answer = r.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                    ws.Cell(wsRow, fixedHeaders.Length + qi + 1).Value = ResolveAnswer(q, answer);
                }

                if (row % 2 == 1)
                    ws.Row(wsRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
            }

            ws.SheetView.FreezeRows(1);

            // AdjustToContents(minWidth, maxWidth) — positional args
            foreach (var col in ws.ColumnsUsed())
                col.AdjustToContents(12, 60);

            if (responses.Count > 0)
            {
                var range = ws.Range(1, 1, responses.Count + 1, allHeaders.Count);
                range.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
                range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CCCCCC");
                range.Style.Border.InsideBorder       = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorderColor  = XLColor.FromHtml("#E0E0E0");
            }
        }

        // ── Sheet 2: Summary ──────────────────────────────────────────────────

        private static void BuildSummarySheet(
            XLWorkbook           wb,
            Survey               survey,
            SurveyVersion        version,
            List<Question>       questions,
            List<SurveyResponse> responses)
        {
            var ws = wb.Worksheets.Add("Summary");

            ws.Cell(1, 1).Value                    = $"Survey Export — {survey.Title}";
            ws.Cell(1, 1).Style.Font.Bold          = true;
            ws.Cell(1, 1).Style.Font.FontSize      = 14;
            ws.Cell(1, 1).Style.Font.FontColor     = XLColor.FromHtml("#3730A3");

            var rows = new List<(string Label, string Value)>
            {
                ("Survey Title",      survey.Title),
                ("Survey ID",         survey.Id.ToString()),
                ("Version",           version.VersionNumber.ToString()),
                ("Status",            survey.Status.ToString()),
                ("Total Responses",   responses.Count.ToString()),
                ("Total Questions",   questions.Count.ToString()),
                ("Exported At (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                ("First Response",    responses.Count > 0
                                        ? responses.Min(r => r.SubmittedAt).ToString("yyyy-MM-dd HH:mm:ss")
                                        : "—"),
                ("Last Response",     responses.Count > 0
                                        ? responses.Max(r => r.SubmittedAt).ToString("yyyy-MM-dd HH:mm:ss")
                                        : "—"),
            };

            for (int i = 0; i < rows.Count; i++)
            {
                int wsRow     = i + 3;
                var labelCell = ws.Cell(wsRow, 1);
                var valueCell = ws.Cell(wsRow, 2);

                labelCell.Value                                = rows[i].Label;
                labelCell.Style.Font.Bold                      = true;
                labelCell.Style.Fill.BackgroundColor           = XLColor.FromHtml("#EEF2FF");
                labelCell.Style.Border.OutsideBorder           = XLBorderStyleValues.Thin;
                labelCell.Style.Border.OutsideBorderColor      = XLColor.FromHtml("#C7D2FE");

                valueCell.Value                                = rows[i].Value;
                valueCell.Style.Border.OutsideBorder           = XLBorderStyleValues.Thin;
                valueCell.Style.Border.OutsideBorderColor      = XLColor.FromHtml("#C7D2FE");
            }

            foreach (var col in ws.ColumnsUsed())
                col.AdjustToContents(20, 50);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void StyleHeaderCell(IXLCell cell, bool isQuestion)
        {
            cell.Style.Font.Bold                 = true;
            cell.Style.Font.FontColor            = XLColor.White;
            cell.Style.Fill.BackgroundColor      = isQuestion
                ? XLColor.FromHtml("#4F46E5")
                : XLColor.FromHtml("#1E293B");
            cell.Style.Alignment.Horizontal      = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#312E81");
        }

        private static string ResolveAnswer(Question question, Answer? answer)
        {
            if (answer is null) return string.Empty;

            return question.Type switch
            {
                QuestionType.SingleChoice =>
                    answer.SelectedOption?.Text ?? string.Empty,

                QuestionType.MultipleChoice when !string.IsNullOrEmpty(answer.SelectedOptionIds) =>
                    string.Join("; ", answer.SelectedOptionIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(idStr => int.TryParse(idStr.Trim(), out var id)
                            ? question.Options.FirstOrDefault(o => o.Id == id)?.Text ?? idStr
                            : idStr)),

                QuestionType.RatingScale => answer.RatingValue?.ToString() ?? string.Empty,
                QuestionType.ShortText   => answer.TextValue   ?? string.Empty,
                QuestionType.LongText    => answer.TextValue   ?? string.Empty,
                _                        => string.Empty
            };
        }
    }
}
