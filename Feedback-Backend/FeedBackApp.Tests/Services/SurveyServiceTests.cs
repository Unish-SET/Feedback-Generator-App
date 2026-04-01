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
    public class SurveyServiceTests
    {
        private Mock<IRepository<Survey>>         _surveyRepo;
        private Mock<IRepository<Question>>       _questionRepo;
        private Mock<IRepository<QuestionOption>> _optionRepo;
        private Mock<IAuditService>               _audit;
        private SurveyService                     _sut;

        [SetUp]
        public void Setup()
        {
            _surveyRepo   = new Mock<IRepository<Survey>>();
            _questionRepo = new Mock<IRepository<Question>>();
            _optionRepo   = new Mock<IRepository<QuestionOption>>();
            _audit        = new Mock<IAuditService>();

            _sut = new SurveyService(
                _surveyRepo.Object,
                _questionRepo.Object,
                _optionRepo.Object,
                _audit.Object);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Survey MakeSurvey(int id = 1, int createdBy = 1, SurveyState state = SurveyState.Inactive) =>
            new Survey
            {
                Id        = id,
                Title     = $"Survey {id}",
                CreatedBy = createdBy,
                State     = state,
                Creator   = new User { Id = createdBy, Username = "owner" }
            };

        private void SetupSurveyQueryable(params Survey[] surveys) =>
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(surveys.ToList().AsQueryable().BuildMock());

        // ── CreateAsync ───────────────────────────────────────────────────────

        [Test]
        public async Task CreateAsync_ShouldReturnDto_WhenDtoIsValid()
        {
            // Arrange
            var dto = new CreateSurveyDto { Title = "My Survey", Description = "Desc", AllowAnonymous = true };
            _surveyRepo.Setup(r => r.AddAsync(It.IsAny<Survey>())).Returns(Task.CompletedTask);
            _surveyRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.CreateAsync(dto, userId: 1);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Title, Is.EqualTo("My Survey"));
            Assert.That(result.State, Is.EqualTo("Inactive"));
        }

        [Test]
        public async Task CreateAsync_ShouldPersistSurvey_WhenDtoIsValid()
        {
            // Arrange
            var dto = new CreateSurveyDto { Title = "T", Description = "", AllowAnonymous = false };
            _surveyRepo.Setup(r => r.AddAsync(It.IsAny<Survey>())).Returns(Task.CompletedTask);
            _surveyRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            await _sut.CreateAsync(dto, userId: 1);

            // Assert
            _surveyRepo.Verify(r => r.AddAsync(It.IsAny<Survey>()), Times.Once);
            _surveyRepo.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Test]
        public async Task CreateAsync_ShouldGenerateUniquePublicToken_WhenCalled()
        {
            // Arrange
            var dto = new CreateSurveyDto { Title = "T", Description = "", AllowAnonymous = false };
            _surveyRepo.Setup(r => r.AddAsync(It.IsAny<Survey>())).Returns(Task.CompletedTask);
            _surveyRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var r1 = await _sut.CreateAsync(dto, userId: 1);
            var r2 = await _sut.CreateAsync(dto, userId: 1);

            // Assert — each survey gets a distinct public token
            Assert.That(r1.PublicToken, Is.Not.EqualTo(r2.PublicToken));
        }

        // ── UpdateAsync ───────────────────────────────────────────────────────

        [Test]
        public void UpdateAsync_ShouldThrowBadRequest_WhenSurveyIsActive()
        {
            // Arrange
            var survey = MakeSurvey(state: SurveyState.Active);
            SetupSurveyQueryable(survey);
            var dto = new UpdateSurveyDto { Title = "New Title", Description = "" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateAsync(1, dto, userId: 1, role: "Creator"));
        }

        [Test]
        public void UpdateAsync_ShouldThrowBadRequest_WhenSurveyIsClosed()
        {
            // Arrange
            var survey = MakeSurvey(state: SurveyState.Closed);
            SetupSurveyQueryable(survey);
            var dto = new UpdateSurveyDto { Title = "New Title", Description = "" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateAsync(1, dto, userId: 1, role: "Creator"));
        }

        [Test]
        public void UpdateAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            var survey = MakeSurvey(createdBy: 99, state: SurveyState.Inactive);
            SetupSurveyQueryable(survey);
            var dto = new UpdateSurveyDto { Title = "Hack", Description = "" };

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.UpdateAsync(1, dto, userId: 1, role: "Creator"));
        }

        [Test]
        public void UpdateAsync_ShouldThrowNotFound_WhenSurveyDoesNotExist()
        {
            // Arrange
            SetupSurveyQueryable(); // empty

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.UpdateAsync(999, new UpdateSurveyDto { Title = "X" }, userId: 1, role: "Admin"));
        }

        // ── GetByIdAsync ──────────────────────────────────────────────────────

        [Test]
        public void GetByIdAsync_ShouldThrowNotFound_WhenSurveyDoesNotExist()
        {
            // Arrange
            SetupSurveyQueryable();

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(999, userId: 1, role: "Admin"));
        }

        [Test]
        public void GetByIdAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            var survey = MakeSurvey(createdBy: 99);
            SetupSurveyQueryable(survey);

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetByIdAsync(1, userId: 1, role: "Creator"));
        }

        [Test]
        public async Task GetByIdAsync_ShouldReturnSurvey_WhenAdminAccessesAnyOwnedSurvey()
        {
            // Arrange — Admin can access surveys they don't own
            var survey = MakeSurvey(createdBy: 99);
            SetupSurveyQueryable(survey);

            // Act
            var result = await _sut.GetByIdAsync(1, userId: 1, role: "Admin");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(1));
        }

        // ── SetStateAsync ─────────────────────────────────────────────────────

        [Test]
        public void SetStateAsync_ShouldThrowBadRequest_WhenStateIsAlreadyCurrent()
        {
            // Arrange
            var survey = MakeSurvey(state: SurveyState.Active);
            SetupSurveyQueryable(survey);
            var dto = new SetSurveyStateDto { State = "Active" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.SetStateAsync(1, dto, userId: 1, role: "Admin"));
        }

        [Test]
        public void SetStateAsync_ShouldThrowBadRequest_WhenStateValueIsInvalid()
        {
            // Arrange
            var survey = MakeSurvey(state: SurveyState.Inactive);
            SetupSurveyQueryable(survey);
            var dto = new SetSurveyStateDto { State = "NotAState" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.SetStateAsync(1, dto, userId: 1, role: "Admin"));
        }

        [Test]
        public void SetStateAsync_ShouldThrowBadRequest_WhenActivatingWithNoQuestions()
        {
            // Arrange
            var survey = MakeSurvey(state: SurveyState.Inactive);
            SetupSurveyQueryable(survey);
            _questionRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Question>().AsQueryable().BuildMock());
            var dto = new SetSurveyStateDto { State = "Active" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.SetStateAsync(1, dto, userId: 1, role: "Admin"));
        }

        // ── DeleteAsync ───────────────────────────────────────────────────────

        [Test]
        public void DeleteAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            var survey = MakeSurvey(createdBy: 99);
            SetupSurveyQueryable(survey);

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.DeleteAsync(1, userId: 1, role: "Creator"));
        }

        [Test]
        public async Task DeleteAsync_ShouldSoftDelete_WhenOwnerDeletes()
        {
            // Arrange
            var survey = MakeSurvey(createdBy: 1);
            SetupSurveyQueryable(survey);
            _surveyRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            await _sut.DeleteAsync(1, userId: 1, role: "Creator");

            // Assert — soft delete sets IsDeleted flag
            Assert.That(survey.IsDeleted, Is.True);
        }

        // ── GetByPublicTokenAsync ─────────────────────────────────────────────

        [Test]
        public void GetByPublicTokenAsync_ShouldThrowNotFound_WhenTokenDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey>().AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByPublicTokenAsync(Guid.NewGuid()));
        }

        [Test]
        public void GetByPublicTokenAsync_ShouldThrowBadRequest_WhenSurveyIsInactive()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = new Survey { Id = 1, PublicToken = token, State = SurveyState.Inactive, Questions = new List<Question>() };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.GetByPublicTokenAsync(token));
            Assert.That(ex.Message, Is.EqualTo("SURVEY_PAUSED"));
        }

        [Test]
        public void GetByPublicTokenAsync_ShouldThrowBadRequest_WhenSurveyIsClosed()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = new Survey { Id = 1, PublicToken = token, State = SurveyState.Closed, Questions = new List<Question>() };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.GetByPublicTokenAsync(token));
            Assert.That(ex.Message, Is.EqualTo("SURVEY_CLOSED"));
        }

        [Test]
        public void GetByPublicTokenAsync_ShouldThrowBadRequest_WhenSurveyHasNoQuestions()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = new Survey { Id = 1, PublicToken = token, State = SurveyState.Active, Questions = new List<Question>() };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.GetByPublicTokenAsync(token));
            Assert.That(ex.Message, Is.EqualTo("SURVEY_NO_QUESTIONS"));
        }

        [Test]
        public void GetByPublicTokenAsync_ShouldThrowBadRequest_WhenSurveyHasNotStartedYet()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = new Survey
            {
                Id = 1, PublicToken = token, State = SurveyState.Active,
                StartDate = DateTime.UtcNow.AddDays(5),
                Questions = new List<Question> { new Question { Id = 1, Text = "Q" } }
            };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.GetByPublicTokenAsync(token));
            Assert.That(ex.Message, Is.EqualTo("SURVEY_NOT_STARTED"));
        }

        [Test]
        public void GetByPublicTokenAsync_ShouldThrowBadRequest_WhenSurveyHasExpired()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = new Survey
            {
                Id = 1, PublicToken = token, State = SurveyState.Active,
                EndDate = DateTime.UtcNow.AddDays(-1),
                Questions = new List<Question> { new Question { Id = 1, Text = "Q" } }
            };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.GetByPublicTokenAsync(token));
            Assert.That(ex.Message, Is.EqualTo("SURVEY_EXPIRED"));
        }
    }
}
