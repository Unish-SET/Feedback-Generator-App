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
    public class QuestionServiceTests
    {
        private Mock<IRepository<Survey>> _surveyRepoMock;
        private Mock<IRepository<SurveyVersion>> _versionRepoMock;
        private Mock<IRepository<Question>> _questionRepoMock;
        private Mock<IRepository<QuestionOption>> _optionRepoMock;
        private QuestionService _questionService;

        [SetUp]
        public void Setup()
        {
            _surveyRepoMock = new Mock<IRepository<Survey>>();
            _versionRepoMock = new Mock<IRepository<SurveyVersion>>();
            _questionRepoMock = new Mock<IRepository<Question>>();
            _optionRepoMock = new Mock<IRepository<QuestionOption>>();

            _questionService = new QuestionService(
                _surveyRepoMock.Object,
                _versionRepoMock.Object,
                _questionRepoMock.Object,
                _optionRepoMock.Object);
        }

        [Test]
        public void AddQuestionAsync_NonDraftSurvey_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Active, CreatedBy = 1 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            var dto = new CreateQuestionDto { Text = "Q1", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _questionService.AddQuestionAsync(1, dto, 1, "Admin"));
        }

        [Test]
        public void AddQuestionAsync_InvalidType_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Draft, CreatedBy = 1 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            var dto = new CreateQuestionDto { Text = "Q1", Type = "InvalidType" };

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _questionService.AddQuestionAsync(1, dto, 1, "Admin"));
            Assert.That(ex.Message, Does.Contain("Invalid question type"));
        }

        [Test]
        public void AddQuestionAsync_SurveyNotFound_ThrowsNotFoundException()
        {
            // Arrange
            _surveyRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Survey?)null);

            var dto = new CreateQuestionDto { Text = "Q1", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _questionService.AddQuestionAsync(999, dto, 1, "Admin"));
        }

        [Test]
        public void AddQuestionAsync_NotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Draft, CreatedBy = 2 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            var dto = new CreateQuestionDto { Text = "Q1", Type = "ShortText" };

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _questionService.AddQuestionAsync(1, dto, 1, "Creator"));
        }

        [Test]
        public void DeleteQuestionAsync_NonDraftSurvey_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Active, CreatedBy = 1 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _questionService.DeleteQuestionAsync(1, 1, 1, "Admin"));
        }

        [Test]
        public void DeleteQuestionAsync_QuestionNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Draft, CreatedBy = 1 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            var questions = new List<Question>();
            _questionRepoMock.Setup(r => r.GetQueryable()).Returns(questions.AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _questionService.DeleteQuestionAsync(1, 999, 1, "Admin"));
        }

        [Test]
        public void UpdateQuestionAsync_InvalidType_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Draft, CreatedBy = 1 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            var dto = new UpdateQuestionDto { Text = "Q1", Type = "InvalidType" };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _questionService.UpdateQuestionAsync(1, 1, dto, 1, "Admin"));
        }
    }
}
