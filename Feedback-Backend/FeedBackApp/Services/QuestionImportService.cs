using ClosedXML.Excel;
using FeedBackApp.Exceptions;
using FeedBackApp.Helpers;
using FeedBackApp.Interfaces;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class QuestionImportService : IQuestionImportService
    {
        // Excel column positions (1-based, matching the template)
        private const int ColText       = 1;
        private const int ColType       = 2;
        private const int ColIsRequired = 3;
        private const int ColOption1    = 4;
        private const int ColOption2    = 5;
        private const int ColOption3    = 6;
        private const int ColOption4    = 7;

        // Accepted type aliases from the Excel (case-insensitive)
        private static readonly Dictionary<string, QuestionType> TypeAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["MCQ"]          = QuestionType.MultipleChoice,
            ["MultipleChoice"] = QuestionType.MultipleChoice,
            ["SingleChoice"] = QuestionType.SingleChoice,
            ["Single"]       = QuestionType.SingleChoice,
            ["Text"]         = QuestionType.ShortText,
            ["ShortText"]    = QuestionType.ShortText,
            ["LongText"]     = QuestionType.LongText,
            ["Rating"]       = QuestionType.RatingScale,
            ["RatingScale"]  = QuestionType.RatingScale,
        };

        private readonly IRepository<Survey>             _surveyRepo;
        private readonly IRepository<Question>           _questionRepo;
        private readonly IRepository<BankQuestion>       _bankRepo;
        private readonly IAuditService                   _audit;

        public QuestionImportService(
            IRepository<Survey>        surveyRepo,
            IRepository<Question>      questionRepo,
            IRepository<BankQuestion>  bankRepo,
            IAuditService              audit)
        {
            _surveyRepo   = surveyRepo;
            _questionRepo = questionRepo;
            _bankRepo     = bankRepo;
            _audit        = audit;
        }

        public async Task<QuestionImportResultDto> ImportAsync(
            Stream fileStream,
            int?   surveyId,
            bool   addToQuestionBank,
            int    userId,
            string role)
        {
            // ── 1. Validate at least one destination is specified ─────────────
            if (surveyId == null && !addToQuestionBank)
                throw new BadRequestException(
                    "Specify a surveyId, set addToQuestionBank=true, or both.");

            // ── 2. Validate survey access and get target version ──────────────
            int nextOrder = 1;

            if (surveyId.HasValue)
            {
                var survey = await _surveyRepo.GetByIdAsync(surveyId.Value);
                if (survey == null)
                    throw new NotFoundException($"Survey {surveyId.Value} not found.");

                if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                    throw new ForbiddenException("You do not have access to this survey.");

                if (survey.State != SurveyState.Inactive)
                    throw new BadRequestException(
                        "Questions can only be imported into an Inactive survey.");

                var maxOrder = await _questionRepo.GetQueryable()
                    .Where(q => q.SurveyId == surveyId.Value)
                    .Select(q => (int?)q.Order)
                    .MaxAsync();

                nextOrder = (maxOrder ?? 0) + 1;
            }

            // ── 3. Parse Excel ────────────────────────────────────────────────
            List<ParsedRow> rows;
            try
            {
                rows = ParseExcel(fileStream);
            }
            catch (Exception ex) when (ex is not AppException)
            {
                throw new BadRequestException(
                    $"Could not read the Excel file. Ensure it matches the template format. ({ex.Message})");
            }

            if (rows.Count == 0)
                throw new BadRequestException("The Excel file contains no data rows.");

            // ── 4. Validate + build entities ──────────────────────────────────
            var result  = new QuestionImportResultDto { Total = rows.Count };
            var toSave  = new List<(Question? surveyQuestion, BankQuestion? bankQuestion)>();

            foreach (var row in rows)
            {
                var rowErrors = ValidateRow(row);
                if (rowErrors.Any())
                {
                    result.Failed++;
                    result.Errors.AddRange(rowErrors);
                    continue;
                }

                var options = BuildOptions(row);

                Question?     surveyQ = null;
                BankQuestion? bankQ   = null;

                if (surveyId.HasValue)
                {
                    surveyQ = new Question
                    {
                        SurveyId   = surveyId.Value,
                        Text       = row.Text!,
                        Type       = row.Type!.Value,
                        IsRequired = row.IsRequired,
                        Order      = nextOrder++,
                        Options    = options
                            .Select((o, i) => new QuestionOption { Text = o, Order = i + 1 })
                            .ToList()
                    };
                }

                if (addToQuestionBank)
                {
                    bankQ = new BankQuestion
                    {
                        CreatedBy  = userId,
                        Text       = row.Text!,
                        Type       = row.Type!.Value,
                        IsRequired = row.IsRequired,
                        CreatedAt  = DateTime.UtcNow,
                        UpdatedAt  = DateTime.UtcNow,
                        Options    = options
                            .Select((o, i) => new BankQuestionOption { Text = o, Order = i + 1 })
                            .ToList()
                    };
                }

                toSave.Add((surveyQ, bankQ));
                result.Success++;
            }

            // ── 5. Persist all valid rows in one SaveChangesAsync ─────────────
            if (toSave.Any())
            {
                foreach (var (sq, bq) in toSave)
                {
                    if (sq != null) await _questionRepo.AddAsync(sq);
                    if (bq != null) await _bankRepo.AddAsync(bq);
                }

                // Both repos share the same DbContext scoped instance — one save covers both
                await _questionRepo.SaveChangesAsync();

                _ = _audit.LogAsync(
                    action:     "ImportQuestions",
                    entityName: surveyId.HasValue ? "Survey" : "QuestionBank",
                    entityId:   surveyId?.ToString() ?? "bank",
                    userId:     userId,
                    newValues:  $"{{\"imported\":{result.Success},\"failed\":{result.Failed}}}");
            }

            return result;
        }

        public byte[] GetTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Questions");

            // Headers
            var headers = new[]
            {
                "QuestionText", "Type", "IsRequired",
                "Option1", "Option2", "Option3", "Option4"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold            = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
                cell.Style.Font.FontColor       = XLColor.White;
            }

            // Example rows
            ws.Cell(2, 1).Value = "What is your overall satisfaction?";
            ws.Cell(2, 2).Value = "Rating";
            ws.Cell(2, 3).Value = "true";

            ws.Cell(3, 1).Value = "Which features do you use?";
            ws.Cell(3, 2).Value = "MCQ";
            ws.Cell(3, 3).Value = "false";
            ws.Cell(3, 4).Value = "Dashboard";
            ws.Cell(3, 5).Value = "Reports";
            ws.Cell(3, 6).Value = "Surveys";
            ws.Cell(3, 7).Value = "Analytics";

            ws.Cell(4, 1).Value = "Any additional comments?";
            ws.Cell(4, 2).Value = "Text";
            ws.Cell(4, 3).Value = "false";

            foreach (var col in ws.ColumnsUsed())
                col.AdjustToContents(15, 60);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static List<ParsedRow> ParseExcel(Stream stream)
        {
            using var wb = new XLWorkbook(stream);
            var ws   = wb.Worksheets.First();
            var rows = new List<ParsedRow>();
            int rowNum = 2; // skip header

            while (true)
            {
                var textCell = ws.Cell(rowNum, ColText);
                // Stop at first completely empty row
                if (textCell.IsEmpty() &&
                    ws.Cell(rowNum, ColType).IsEmpty() &&
                    ws.Cell(rowNum, ColIsRequired).IsEmpty())
                    break;

                rows.Add(new ParsedRow
                {
                    RowNumber  = rowNum,
                    RawText    = CellString(ws, rowNum, ColText),
                    RawType    = CellString(ws, rowNum, ColType),
                    RawRequired = CellString(ws, rowNum, ColIsRequired),
                    RawOptions = new[]
                    {
                        CellString(ws, rowNum, ColOption1),
                        CellString(ws, rowNum, ColOption2),
                        CellString(ws, rowNum, ColOption3),
                        CellString(ws, rowNum, ColOption4),
                    }
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o!)
                    .ToList()
                });

                rowNum++;
                if (rowNum > 10_000) break; // safety cap
            }

            return rows;
        }

        private static string CellString(IXLWorksheet ws, int row, int col)
            => ws.Cell(row, col).GetValue<string>()?.Trim() ?? string.Empty;

        private static List<string> ValidateRow(ParsedRow row)
        {
            var errors = new List<string>();
            string prefix = $"Row {row.RowNumber}:";

            if (string.IsNullOrWhiteSpace(row.RawText))
                errors.Add($"{prefix} Question text is required.");

            if (string.IsNullOrWhiteSpace(row.RawType))
            {
                errors.Add($"{prefix} Type is required (MCQ, Text, Rating).");
                return errors; // can't validate further without type
            }

            if (!TypeAliases.TryGetValue(row.RawType, out var parsedType))
            {
                errors.Add($"{prefix} Unknown type '{row.RawType}'. Valid values: MCQ, SingleChoice, Text, LongText, Rating.");
                return errors;
            }

            row.Text       = row.RawText.Trim();
            row.Type       = parsedType;
            row.IsRequired = row.RawRequired.Equals("true", StringComparison.OrdinalIgnoreCase);

            var isChoice = parsedType == QuestionType.MultipleChoice ||
                           parsedType == QuestionType.SingleChoice;

            if (isChoice && row.RawOptions.Count < 2)
                errors.Add($"{prefix} Choice questions require at least 2 options.");

            return errors;
        }

        private static List<string> BuildOptions(ParsedRow row)
        {
            var isChoice = row.Type == QuestionType.MultipleChoice ||
                           row.Type == QuestionType.SingleChoice;

            return isChoice ? row.RawOptions : new List<string>();
        }

        // ── Internal parse model ──────────────────────────────────────────────

        private sealed class ParsedRow
        {
            public int          RowNumber   { get; set; }
            public string       RawText     { get; set; } = string.Empty;
            public string       RawType     { get; set; } = string.Empty;
            public string       RawRequired { get; set; } = string.Empty;
            public List<string> RawOptions  { get; set; } = new();

            // Populated after validation
            public string?      Text       { get; set; }
            public QuestionType? Type      { get; set; }
            public bool         IsRequired { get; set; }
        }
    }
}
