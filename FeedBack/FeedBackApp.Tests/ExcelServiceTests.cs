using MockQueryable.Moq;
using ClosedXML.Excel;
using Moq;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using FeedBackApp.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeedBackApp.Tests
{
    [TestFixture]
    public class ExcelServiceTests
    {
        private Mock<IRepository<Survey>>         _surveyRepoMock;
        private Mock<IRepository<SurveyVersion>>  _versionRepoMock;
        private Mock<IRepository<SurveyResponse>> _responseRepoMock;
        private ExcelService                      _excelService;

        [SetUp]
        public void Setup()
        {
            _surveyRepoMock   = new Mock<IRepository<Survey>>();
            _versionRepoMock  = new Mock<IRepository<SurveyVersion>>();
            _responseRepoMock = new Mock<IRepository<SurveyResponse>>();

            _excelService = new ExcelService(
                _surveyRepoMock.Object,
                _versionRepoMock.Object,
                _responseRepoMock.Object,
                NullLogger<ExcelService>.Instance);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Survey MakeSurvey(int id = 1, int createdBy = 1) => new Survey
        {
            Id = id, Title = $"Survey {id}", CreatedBy = createdBy, Status = SurveyStatus.Active
        };

        private static SurveyVersion MakeVersion(int surveyId, params Question[] questions) =>
            new SurveyVersion
            {
                Id = 1, SurveyId = surveyId, VersionNumber = 1,
                Questions = questions.ToList()
            };

        private static Question MakeQuestion(int id, QuestionType type,
            string text = "", params QuestionOption[] opts) => new Question
        {
            Id = id, Text = string.IsNullOrEmpty(text) ? $"Question {id}" : text,
            Type = type, IsRequired = true, Order = id,
            Options = opts.ToList(), Answers = new List<Answer>()
        };

        private static QuestionOption MakeOption(int id, string text) =>
            new QuestionOption { Id = id, Text = text };

        private static SurveyResponse MakeResponse(int id, int versionId,
            User? user, params Answer[] answers) => new SurveyResponse
        {
            Id = id, SurveyVersionId = versionId, User = user, UserId = user?.Id,
            SubmittedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            Answers = answers.ToList()
        };

        private static User MakeUser(int id, string username) =>
            new User { Id = id, Username = username };

        private void SetupVersion(SurveyVersion version) =>
            _versionRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<SurveyVersion> { version }.AsQueryable().BuildMock());

        private void SetupEmptyVersions() =>
            _versionRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<SurveyVersion>().AsQueryable().BuildMock());

        private void SetupResponses(params SurveyResponse[] responses) =>
            _responseRepoMock.Setup(r => r.GetQueryable())
                .Returns(responses.ToList().AsQueryable().BuildMock());

        // ── Authorization ─────────────────────────────────────────────────────

        [Test]
        public void ExportExcelAsync_SurveyNotFound_ThrowsNotFoundException()
        {
            _surveyRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Survey?)null);

            Assert.ThrowsAsync<NotFoundException>(
                () => _excelService.ExportExcelAsync(99, 1, "Creator"));
        }

        [Test]
        public void ExportExcelAsync_CreatorNotOwner_ThrowsForbiddenException()
        {
            var survey = MakeSurvey(createdBy: 5);
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            Assert.ThrowsAsync<ForbiddenException>(
                () => _excelService.ExportExcelAsync(1, userId: 2, role: "Creator"));
        }

        [Test]
        public void ExportExcelAsync_AdminBypassesOwnership_HitsNoVersionInstead()
        {
            var survey = MakeSurvey(createdBy: 5);
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupEmptyVersions();

            // Admin bypasses owner check, but no version exists -> BadRequest
            Assert.ThrowsAsync<BadRequestException>(
                () => _excelService.ExportExcelAsync(1, userId: 1, role: "Admin"));
        }

        [Test]
        public void ExportExcelAsync_NoVersion_ThrowsBadRequestWithCorrectMessage()
        {
            var survey = MakeSurvey(createdBy: 1);
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupEmptyVersions();

            var ex = Assert.ThrowsAsync<BadRequestException>(
                () => _excelService.ExportExcelAsync(1, 1, "Creator"));

            Assert.That(ex.Message, Is.EqualTo("Survey has no published version."));
        }

        // ── Return value ──────────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_ValidData_ReturnsBytesGreaterThanZero()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var version = MakeVersion(1, MakeQuestion(1, QuestionType.ShortText));
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses();

            var result = await _excelService.ExportExcelAsync(1, 1, "Creator");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task ExportExcelAsync_ValidData_ReturnsValidXlsxFile()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var version = MakeVersion(1, MakeQuestion(1, QuestionType.ShortText));
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses();

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");

            // Must be valid xlsx that ClosedXML can open
            using var ms = new MemoryStream(bytes);
            Assert.DoesNotThrow(() => new XLWorkbook(ms));
        }

        // ── Sheet structure ───────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_WorkbookHasResponsesAndSummarySheets()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var version = MakeVersion(1, MakeQuestion(1, QuestionType.ShortText));
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses();

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);

            Assert.That(wb.Worksheets.Contains("Responses"), Is.True);
            Assert.That(wb.Worksheets.Contains("Summary"),   Is.True);
        }

        [Test]
        public async Task ExportExcelAsync_NoResponses_OnlyHeaderRowInSheet()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var version = MakeVersion(1, MakeQuestion(1, QuestionType.ShortText, "City"));
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses();

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet("Responses");

            Assert.That(ws.LastRowUsed()?.RowNumber() ?? 0, Is.EqualTo(1));
        }

        [Test]
        public async Task ExportExcelAsync_TwoResponses_HasHeaderPlusTwoRows()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var q       = MakeQuestion(1, QuestionType.ShortText, "Name");
            var version = MakeVersion(1, q);

            var r1 = MakeResponse(1, 1, MakeUser(10, "alice"),
                new Answer { QuestionId = 1, TextValue = "Alice" });
            var r2 = MakeResponse(2, 1, MakeUser(11, "bob"),
                new Answer { QuestionId = 1, TextValue = "Bob" });

            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses(r1, r2);

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet("Responses");

            Assert.That(ws.LastRowUsed()!.RowNumber(), Is.EqualTo(3)); // header + 2
        }

        // ── Header correctness ────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_HeaderContainsFixedAndQuestionColumns()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var version = MakeVersion(1,
                MakeQuestion(1, QuestionType.ShortText, "Age"),
                MakeQuestion(2, QuestionType.RatingScale, "Rating"));
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses();

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var ws   = wb.Worksheet("Responses");
            var cols = ws.LastColumnUsed()!.ColumnNumber();
            var headers = Enumerable.Range(1, cols)
                .Select(c => ws.Cell(1, c).GetValue<string>())
                .ToList();

            Assert.That(headers, Does.Contain("ResponseId"));
            Assert.That(headers, Does.Contain("UserName"));
            Assert.That(headers, Does.Contain("SubmittedAt"));
            Assert.That(headers, Does.Contain("Age"));
            Assert.That(headers, Does.Contain("Rating"));
        }

        [Test]
        public async Task ExportExcelAsync_ColumnCountEqualsThreePlusQuestionCount()
        {
            const int qCount = 3;
            var survey  = MakeSurvey(createdBy: 1);
            var questions = Enumerable.Range(1, qCount)
                .Select(i => MakeQuestion(i, QuestionType.ShortText)).ToArray();
            var version = MakeVersion(1, questions);
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses();

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet("Responses");

            Assert.That(ws.LastColumnUsed()!.ColumnNumber(), Is.EqualTo(3 + qCount));
        }

        // ── Answer type resolution ────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_ShortTextAnswer_WrittenCorrectly()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var q       = MakeQuestion(1, QuestionType.ShortText, "City");
            var version = MakeVersion(1, q);
            var resp    = MakeResponse(1, 1, MakeUser(1, "carol"),
                new Answer { QuestionId = 1, TextValue = "Chennai" });

            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses(resp);

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");
            using var wb = new XLWorkbook(new MemoryStream(bytes));

            Assert.That(wb.Worksheet("Responses").Cell(2, 4).GetValue<string>(), Is.EqualTo("Chennai"));
        }

        [Test]
        public async Task ExportExcelAsync_RatingAnswer_WrittenAsString()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var q       = MakeQuestion(1, QuestionType.RatingScale, "Stars");
            var version = MakeVersion(1, q);
            var resp    = MakeResponse(1, 1, MakeUser(1, "dave"),
                new Answer { QuestionId = 1, RatingValue = 4 });

            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses(resp);

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");
            using var wb = new XLWorkbook(new MemoryStream(bytes));

            Assert.That(wb.Worksheet("Responses").Cell(2, 4).GetValue<string>(), Is.EqualTo("4"));
        }

        [Test]
        public async Task ExportExcelAsync_SingleChoiceAnswer_WritesOptionText()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var opt     = MakeOption(10, "Yes");
            var q       = MakeQuestion(1, QuestionType.SingleChoice, "Agree?", opt);
            var version = MakeVersion(1, q);
            var resp    = MakeResponse(1, 1, MakeUser(1, "eve"),
                new Answer { QuestionId = 1, SelectedOptionId = 10, SelectedOption = opt });

            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses(resp);

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");
            using var wb = new XLWorkbook(new MemoryStream(bytes));

            Assert.That(wb.Worksheet("Responses").Cell(2, 4).GetValue<string>(), Is.EqualTo("Yes"));
        }

        [Test]
        public async Task ExportExcelAsync_AnonymousUser_WritesAnonymous()
        {
            var survey  = MakeSurvey(createdBy: 1);
            var version = MakeVersion(1, MakeQuestion(1, QuestionType.ShortText));
            var resp    = MakeResponse(1, 1, user: null);

            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses(resp);

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");
            using var wb = new XLWorkbook(new MemoryStream(bytes));

            Assert.That(wb.Worksheet("Responses").Cell(2, 2).GetValue<string>(), Is.EqualTo("Anonymous"));
        }

        // ── Summary sheet ─────────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_SummarySheet_ContainsSurveyTitle()
        {
            var survey       = MakeSurvey(createdBy: 1);
            survey.Title     = "Customer NPS 2026";
            var version      = MakeVersion(1, MakeQuestion(1, QuestionType.ShortText));
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupVersion(version);
            SetupResponses();

            var bytes = await _excelService.ExportExcelAsync(1, 1, "Creator");
            using var wb = new XLWorkbook(new MemoryStream(bytes));

            var title = wb.Worksheet("Summary").Cell(1, 1).GetValue<string>();
            Assert.That(title, Does.Contain("Customer NPS 2026"));
        }
    }
}
