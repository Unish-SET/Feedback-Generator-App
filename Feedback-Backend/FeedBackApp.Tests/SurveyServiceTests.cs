using MockQueryable.Moq;
using Moq;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using FeedBackApp.Services;

namespace FeedBackApp.Tests
{
    [TestFixture]
    public class SurveyServiceTests
    {
        private Mock<IRepository<Survey>> _surveyRepoMock;
        private Mock<IRepository<SurveyVersion>> _versionRepoMock;
        private Mock<IRepository<Question>> _questionRepoMock;
        private Mock<IRepository<QuestionOption>> _optionRepoMock;
        private Mock<IRepository<User>> _userRepoMock;
        private SurveyService _surveyService;

        [SetUp]
        public void Setup()
        {
            _surveyRepoMock = new Mock<IRepository<Survey>>();
            _versionRepoMock = new Mock<IRepository<SurveyVersion>>();
            _questionRepoMock = new Mock<IRepository<Question>>();
            _optionRepoMock = new Mock<IRepository<QuestionOption>>();
            _userRepoMock = new Mock<IRepository<User>>();

            _surveyService = new SurveyService(
                _surveyRepoMock.Object,
                _versionRepoMock.Object,
                _questionRepoMock.Object,
                _optionRepoMock.Object,
                _userRepoMock.Object);
        }

        [Test]
        public async Task CreateAsync_ValidDto_CreatesSurveyAndVersion()
        {
            // Arrange
            var dto = new CreateSurveyDto
            {
                Title = "Test Survey",
                Description = "A test survey",
                AllowAnonymous = true
            };
            int userId = 1;

            _surveyRepoMock.Setup(r => r.AddAsync(It.IsAny<Survey>())).Returns(Task.CompletedTask);
            _surveyRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<SurveyVersion>())).Returns(Task.CompletedTask);
            _versionRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var versions = new List<SurveyVersion> { new SurveyVersion { Id = 1, SurveyId = 1, VersionNumber = 1 } };
            _versionRepoMock.Setup(r => r.GetQueryable()).Returns(versions.AsQueryable().BuildMock());

            var user = new User { Id = 1, Username = "testuser", Email = "test@test.com" };
            _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _surveyService.CreateAsync(dto, userId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Title, Is.EqualTo("Test Survey"));
            _surveyRepoMock.Verify(r => r.AddAsync(It.IsAny<Survey>()), Times.Once);
            _versionRepoMock.Verify(r => r.AddAsync(It.IsAny<SurveyVersion>()), Times.Once);
        }

        [Test]
        public void UpdateAsync_NonDraftSurvey_ThrowsBadRequestException()
        {
            // Arrange
            var activeSurvey = new Survey
            {
                Id = 1, Title = "Active Survey", Status = SurveyStatus.Active,
                CreatedBy = 1, Creator = new User { Id = 1, Username = "owner" },
                Versions = new List<SurveyVersion>()
            };

            var surveys = new List<Survey> { activeSurvey };
            _surveyRepoMock.Setup(r => r.GetQueryable()).Returns(surveys.AsQueryable().BuildMock());

            var dto = new UpdateSurveyDto { Title = "Updated", Description = "" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _surveyService.UpdateAsync(1, dto, 1, "Creator"));
        }

        [Test]
        public void GetByIdAsync_SurveyNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var surveys = new List<Survey>();
            _surveyRepoMock.Setup(r => r.GetQueryable()).Returns(surveys.AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _surveyService.GetByIdAsync(999, 1, "Admin"));
        }

        [Test]
        public void GetByIdAsync_NotOwnerNotAdmin_ThrowsForbiddenException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Title = "Test", Status = SurveyStatus.Draft,
                CreatedBy = 2, Creator = new User { Id = 2, Username = "otheruser" },
                Versions = new List<SurveyVersion>()
            };

            var surveys = new List<Survey> { survey };
            _surveyRepoMock.Setup(r => r.GetQueryable()).Returns(surveys.AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _surveyService.GetByIdAsync(1, 1, "Creator"));
        }

        [Test]
        public void PublishAsync_AlreadyActive_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Title = "Active", Status = SurveyStatus.Active,
                CreatedBy = 1, Creator = new User { Id = 1, Username = "owner" },
                Versions = new List<SurveyVersion>()
            };

            var surveys = new List<Survey> { survey };
            _surveyRepoMock.Setup(r => r.GetQueryable()).Returns(surveys.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _surveyService.PublishAsync(1, 1, "Admin"));
            Assert.That(ex.Message, Is.EqualTo("Survey is already active."));
        }

        [Test]
        public void PublishAsync_ClosedSurvey_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Title = "Closed", Status = SurveyStatus.Closed,
                CreatedBy = 1, Creator = new User { Id = 1, Username = "owner" },
                Versions = new List<SurveyVersion>()
            };

            var surveys = new List<Survey> { survey };
            _surveyRepoMock.Setup(r => r.GetQueryable()).Returns(surveys.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _surveyService.PublishAsync(1, 1, "Admin"));
            Assert.That(ex.Message, Does.Contain("Closed surveys"));
        }

        [Test]
        public void CloseAsync_AlreadyClosed_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Title = "Closed", Status = SurveyStatus.Closed,
                CreatedBy = 1, Creator = new User { Id = 1, Username = "owner" },
                Versions = new List<SurveyVersion>()
            };

            var surveys = new List<Survey> { survey };
            _surveyRepoMock.Setup(r => r.GetQueryable()).Returns(surveys.AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _surveyService.CloseAsync(1, 1, "Admin"));
        }

        [Test]
        public void UnpublishAsync_NotActive_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Title = "Draft", Status = SurveyStatus.Draft,
                CreatedBy = 1, Creator = new User { Id = 1, Username = "owner" },
                Versions = new List<SurveyVersion>()
            };

            var surveys = new List<Survey> { survey };
            _surveyRepoMock.Setup(r => r.GetQueryable()).Returns(surveys.AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _surveyService.UnpublishAsync(1, 1, "Admin"));
        }
    }
}
