using MockQueryable.Moq;
using Moq;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Services;

namespace FeedBackApp.Tests.Services
{
    [TestFixture]
    public class QuestionServiceTests
    {
        private Mock<IRepository<Survey>>         _surveyRepo;
        private Mock<IRepository<Question>>       _questionRepo;
        private Mock<IRepository<QuestionOption>> _optionRepo;
        private Mock<IAuditService>               _audit;
        private Mock<IQuestionBankService>        _bankService;
        private QuestionService                   _sut;

        [SetUp]
        public void Setup()
        {
            _surveyRepo   = new Mock<IRepository<Survey>>();
            _questionRepo = new Mock<IRepository<Question>>();
            _optionRepo   = new Mock<IRepository<QuestionOption>>();
            _audit        = new Mock<IAuditService>();
            _bankService  = new Mock<IQuestionBankService>();

            _sut = new QuestionService(
                _surveyRepo.Object,
                _questionRepo.Object,
                _optionRepo.Object,
                _audit.Object,
                _bankService.Object);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Survey MakeSurvey(int id = 1, int createdBy = 1, SurveyState state = SurveyState.Inactive) =>
            new Survey { Id = id, CreatedBy = createdBy, State = state };

        // ── AddQuestionAsync ──────────────────────────────────────────────────

        [Test]
        public void AddQuestionAsync_ShouldThrowNotFound_WhenSurveyDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Survey?)null);
            var dto = new CreateQuestionDto { Text = "Q", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.AddQuestionAsync(999, dto, userId: 1, role: "Admin"));
        }

        [Test]
        public void AddQuestionAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey(createdBy: 99));
            var dto = new CreateQuestionDto { Text = "Q", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.AddQuestionAsync(1, dto, userId: 1, role: "Creator"));
        }

        [Test]
        public void AddQuestionAsync_ShouldThrowBadRequest_WhenSurveyIsActive()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey(state: SurveyState.Active));
            var dto = new CreateQuestionDto { Text = "Q", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.AddQuestionAsync(1, dto, userId: 1, role: "Admin"));
        }

        [Test]
        public void AddQuestionAsync_ShouldThrowBadRequest_WhenQuestionTypeIsInvalid()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            var dto = new CreateQuestionDto { Text = "Q", Type = "NotAType" };

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.AddQuestionAsync(1, dto, userId: 1, role: "Admin"));
            Assert.That(ex.Message, Does.Contain("Invalid question type"));
        }

        [Test]
        public async Task AddQuestionAsync_ShouldPersistQuestion_WhenInputIsValid()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            _questionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Capture the question passed to AddAsync so we can return it from GetQueryable.
            // The service calls MapToResponseDto(question.Id) after AddAsync — since there is
            // no real DB, question.Id stays 0. We return a queryable that contains that same
            // question (Id = 0) so FirstAsync(q => q.Id == 0) finds it.
            Question? added = null;
            _questionRepo
                .Setup(r => r.AddAsync(It.IsAny<Question>()))
                .Callback<Question>(q => added = q)
                .Returns(Task.CompletedTask);

            _questionRepo.Setup(r => r.GetQueryable())
                .Returns(() => new List<Question> { added! }.AsQueryable().BuildMock());

            _bankService.Setup(b => b.AutoSaveQuestionsAsync(It.IsAny<IEnumerable<Question>>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var dto = new CreateQuestionDto { Text = "Q", Type = "ShortText", Order = 1 };

            // Act
            var result = await _sut.AddQuestionAsync(1, dto, userId: 1, role: "Admin");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Text, Is.EqualTo("Q"));
            _questionRepo.Verify(r => r.AddAsync(It.IsAny<Question>()), Times.Once);
        }

        // ── UpdateQuestionAsync ───────────────────────────────────────────────

        [Test]
        public void UpdateQuestionAsync_ShouldThrowBadRequest_WhenSurveyIsActive()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey(state: SurveyState.Active));
            var dto = new UpdateQuestionDto { Text = "Q", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateQuestionAsync(1, 1, dto, userId: 1, role: "Admin"));
        }

        [Test]
        public void UpdateQuestionAsync_ShouldThrowBadRequest_WhenTypeIsInvalid()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            var dto = new UpdateQuestionDto { Text = "Q", Type = "BadType" };

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateQuestionAsync(1, 1, dto, userId: 1, role: "Admin"));
            Assert.That(ex.Message, Does.Contain("Invalid question type"));
        }

        [Test]
        public void UpdateQuestionAsync_ShouldThrowNotFound_WhenQuestionDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            _questionRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Question>().AsQueryable().BuildMock());
            var dto = new UpdateQuestionDto { Text = "Q", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateQuestionAsync(1, 999, dto, userId: 1, role: "Admin"));
        }

        // ── DeleteQuestionAsync ───────────────────────────────────────────────

        [Test]
        public void DeleteQuestionAsync_ShouldThrowBadRequest_WhenSurveyIsActive()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey(state: SurveyState.Active));

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteQuestionAsync(1, 1, userId: 1, role: "Admin"));
        }

        [Test]
        public void DeleteQuestionAsync_ShouldThrowNotFound_WhenQuestionDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            _questionRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Question>().AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteQuestionAsync(1, 999, userId: 1, role: "Admin"));
        }

        [Test]
        public void DeleteQuestionAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey(createdBy: 99));

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.DeleteQuestionAsync(1, 1, userId: 1, role: "Creator"));
        }

        // ── GetQuestionsAsync ─────────────────────────────────────────────────

        [Test]
        public void GetQuestionsAsync_ShouldThrowNotFound_WhenSurveyDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Survey?)null);

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.GetQuestionsAsync(999, userId: 1, role: "Admin"));
        }

        [Test]
        public async Task GetQuestionsAsync_ShouldReturnQuestionsOrderedByOrder_WhenSurveyExists()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeSurvey());
            var questions = new List<Question>
            {
                new Question { Id = 2, SurveyId = 1, Text = "B", Type = QuestionType.ShortText, Order = 2, Options = new List<QuestionOption>() },
                new Question { Id = 1, SurveyId = 1, Text = "A", Type = QuestionType.ShortText, Order = 1, Options = new List<QuestionOption>() }
            };
            _questionRepo.Setup(r => r.GetQueryable()).Returns(questions.AsQueryable().BuildMock());

            // Act
            var result = await _sut.GetQuestionsAsync(1, userId: 1, role: "Admin");

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Order, Is.LessThan(result[1].Order));
        }
    }
}
