using ClosedXML.Excel;
using MockQueryable.Moq;
using Moq;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Models;
using FeedBackApp.Models.Enums;
using FeedBackApp.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeedBackApp.Tests.Services
{
    [TestFixture]
    public class ExcelServiceTests
    {
        private Mock<IRepository<Survey>>         _surveyRepo;
        private Mock<IRepository<Question>>       _questionRepo;
        private Mock<IRepository<SurveyResponse>> _responseRepo;
        private ExcelService                      _sut;

        [SetUp]
        public void Setup()
        {
            _surveyRepo   = new Mock<IRepository<Survey>>();
            _questionRepo = new Mock<IRepository<Question>>();
            _responseRepo = new Mock<IRepository<SurveyResponse>>();

            _sut = new ExcelService(
                _surveyRepo.Object,
                _questionRepo.Object,
                _responseRepo.Object,
                NullLogger<ExcelService>.Instance);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Survey MakeSurvey(int id = 1, int createdBy = 1, string title = "Survey") =>
            new Survey { Id = id, Title = title, CreatedBy = createdBy, State = SurveyState.Active };

        private static Question MakeQuestion(int id, QuestionType type, string text = "") =>
            new Question
            {
                Id = id, SurveyId = 1,
                Text = string.IsNullOrEmpty(text) ? $"Question {id}" : text,
                Type = type, Order = id,
                Options = new List<QuestionOption>(),
                Answers = new List<Answer>()
            };

        private static QuestionOption MakeOption(int id, string text) =>
            new QuestionOption { Id = id, Text = text };

        private static SurveyResponse MakeResponse(int id, User? user, params Answer[] answers) =>
            new SurveyResponse
            {
                Id = id, SurveyId = 1, User = user, UserId = user?.Id,
                SubmittedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                Answers = answers.ToList()
            };

        private static User MakeUser(int id, string username) =>
            new User { Id = id, Username = username };

        private void SetupQuestions(params Question[] questions) =>
            _questionRepo.Setup(r => r.GetQueryable())
                .Returns(questions.ToList().AsQueryable().BuildMock());

        private void SetupResponses(params SurveyResponse[] responses) =>
            _responseRepo.Setup(r => r.GetQueryable())
                .Returns(responses.ToList().AsQueryable().BuildMock());

        // ── Access control ────────────────────────────────────────────────────

        [Test]
        public void ExportExcelAsync_ShouldThrowNotFound_WhenSurveyDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Survey?)null);

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.ExportExcelAsync(99, userId: 1, role: "Creator"));
        }

        [Test]
        public void ExportExcelAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey(createdBy: 99));

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.ExportExcelAsync(1, userId: 1, role: "Creator"));
        }

        [Test]
        public async Task ExportExcelAsync_ShouldNotThrowForbidden_WhenAdminExportsAnysurvey()
        {
            // Arrange — Admin bypasses ownership check
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey(createdBy: 99));
            SetupQuestions();
            SetupResponses();

            // Act & Assert — no exception
            var result = await _sut.ExportExcelAsync(1, userId: 1, role: "Admin");
            Assert.That(result.Length, Is.GreaterThan(0));
        }

        // ── Return value ──────────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_ShouldReturnNonEmptyByteArray_WhenExportSucceeds()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText));
            SetupResponses();

            // Act
            var result = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task ExportExcelAsync_ShouldReturnValidXlsxFile_WhenExportSucceeds()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText));
            SetupResponses();

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert — ClosedXML can open the file without throwing
            using var ms = new MemoryStream(bytes);
            Assert.DoesNotThrow(() => new XLWorkbook(ms));
        }

        // ── Sheet structure ───────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_ShouldContainResponsesAndSummarySheets()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText));
            SetupResponses();

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            Assert.That(wb.Worksheets.Contains("Responses"), Is.True);
            Assert.That(wb.Worksheets.Contains("Summary"),   Is.True);
        }

        [Test]
        public async Task ExportExcelAsync_ShouldHaveOnlyHeaderRow_WhenNoResponsesExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText, "City"));
            SetupResponses();

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet("Responses");
            Assert.That(ws.LastRowUsed()?.RowNumber() ?? 0, Is.EqualTo(1));
        }

        [Test]
        public async Task ExportExcelAsync_ShouldHaveHeaderPlusTwoDataRows_WhenTwoResponsesExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText, "Name"));
            SetupResponses(
                MakeResponse(1, MakeUser(10, "alice"), new Answer { QuestionId = 1, TextValue = "Alice" }),
                MakeResponse(2, MakeUser(11, "bob"),   new Answer { QuestionId = 1, TextValue = "Bob" })
            );

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            Assert.That(wb.Worksheet("Responses").LastRowUsed()!.RowNumber(), Is.EqualTo(3));
        }

        // ── Header correctness ────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_ShouldIncludeFixedAndQuestionColumnsInHeader()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(
                MakeQuestion(1, QuestionType.ShortText,  "Age"),
                MakeQuestion(2, QuestionType.RatingScale, "Rating")
            );
            SetupResponses();

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var ws      = wb.Worksheet("Responses");
            var colCount = ws.LastColumnUsed()!.ColumnNumber();
            var headers  = Enumerable.Range(1, colCount)
                .Select(c => ws.Cell(1, c).GetValue<string>())
                .ToList();

            Assert.That(headers, Does.Contain("ResponseId"));
            Assert.That(headers, Does.Contain("UserName"));
            Assert.That(headers, Does.Contain("SubmittedAt"));
            Assert.That(headers, Does.Contain("Age"));
            Assert.That(headers, Does.Contain("Rating"));
        }

        [Test]
        public async Task ExportExcelAsync_ShouldHaveThreePlusQuestionCountColumns()
        {
            // Arrange
            const int qCount = 4;
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(Enumerable.Range(1, qCount).Select(i => MakeQuestion(i, QuestionType.ShortText)).ToArray());
            SetupResponses();

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            Assert.That(wb.Worksheet("Responses").LastColumnUsed()!.ColumnNumber(), Is.EqualTo(3 + qCount));
        }

        // ── Answer type resolution ────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_ShouldWriteTextValue_ForShortTextAnswer()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText, "City"));
            SetupResponses(MakeResponse(1, MakeUser(1, "carol"), new Answer { QuestionId = 1, TextValue = "Chennai" }));

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            Assert.That(wb.Worksheet("Responses").Cell(2, 4).GetValue<string>(), Is.EqualTo("Chennai"));
        }

        [Test]
        public async Task ExportExcelAsync_ShouldWriteRatingAsString_ForRatingScaleAnswer()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.RatingScale, "Stars"));
            SetupResponses(MakeResponse(1, MakeUser(1, "dave"), new Answer { QuestionId = 1, RatingValue = 4 }));

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            Assert.That(wb.Worksheet("Responses").Cell(2, 4).GetValue<string>(), Is.EqualTo("4"));
        }

        [Test]
        public async Task ExportExcelAsync_ShouldWriteOptionText_ForSingleChoiceAnswer()
        {
            // Arrange
            var opt = MakeOption(10, "Yes");
            var q   = MakeQuestion(1, QuestionType.SingleChoice, "Agree?");
            q.Options.Add(opt);

            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(q);
            SetupResponses(MakeResponse(1, MakeUser(1, "eve"),
                new Answer { QuestionId = 1, SelectedOptionId = 10, SelectedOption = opt }));

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            Assert.That(wb.Worksheet("Responses").Cell(2, 4).GetValue<string>(), Is.EqualTo("Yes"));
        }

        [Test]
        public async Task ExportExcelAsync_ShouldWriteAnonymous_WhenRespondentIsAnonymous()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText));
            SetupResponses(MakeResponse(1, user: null));

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            Assert.That(wb.Worksheet("Responses").Cell(2, 2).GetValue<string>(), Is.EqualTo("Anonymous"));
        }

        // ── Summary sheet ─────────────────────────────────────────────────────

        [Test]
        public async Task ExportExcelAsync_ShouldIncludeSurveyTitleInSummarySheet()
        {
            // Arrange
            var survey = MakeSurvey(title: "Customer NPS 2026");
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            SetupQuestions(MakeQuestion(1, QuestionType.ShortText));
            SetupResponses();

            // Act
            var bytes = await _sut.ExportExcelAsync(1, userId: 1, role: "Creator");

            // Assert
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            var title = wb.Worksheet("Summary").Cell(1, 1).GetValue<string>();
            Assert.That(title, Does.Contain("Customer NPS 2026"));
        }
    }
}
