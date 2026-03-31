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
    public class ResponseServiceTests
    {
        private Mock<IRepository<Survey>> _surveyRepoMock;
        private Mock<IRepository<SurveyVersion>> _versionRepoMock;
        private Mock<IRepository<SurveyResponse>> _responseRepoMock;
        private Mock<IRepository<Answer>> _answerRepoMock;
        private ResponseService _responseService;

        [SetUp]
        public void Setup()
        {
            _surveyRepoMock = new Mock<IRepository<Survey>>();
            _versionRepoMock = new Mock<IRepository<SurveyVersion>>();
            _responseRepoMock = new Mock<IRepository<SurveyResponse>>();
            _answerRepoMock = new Mock<IRepository<Answer>>();

            _responseService = new ResponseService(
                _surveyRepoMock.Object,
                _versionRepoMock.Object,
                _responseRepoMock.Object,
                _answerRepoMock.Object);
        }

        [Test]
        public void SubmitAsync_SurveyNotFound_ThrowsNotFoundException()
        {
            // Arrange
            _surveyRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Survey, bool>>>()))
                .ReturnsAsync((Survey?)null);

            var dto = new SubmitResponseDto { SurveyVersionId = 1, Answers = new List<SubmitAnswerDto>() };

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _responseService.SubmitAsync(Guid.NewGuid(), dto, null));
        }

        [Test]
        public void SubmitAsync_SurveyNotActive_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Draft, PublicToken = Guid.NewGuid() };
            _surveyRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Survey, bool>>>()))
                .ReturnsAsync(survey);

            var dto = new SubmitResponseDto { SurveyVersionId = 1, Answers = new List<SubmitAnswerDto>() };

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _responseService.SubmitAsync(survey.PublicToken, dto, null));
            Assert.That(ex.Message, Does.Contain("not currently accepting"));
        }

        [Test]
        public void SubmitAsync_SurveyNotStarted_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Status = SurveyStatus.Active,
                StartDate = DateTime.UtcNow.AddDays(5),
                PublicToken = Guid.NewGuid(),
                AllowAnonymous = true
            };
            _surveyRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Survey, bool>>>()))
                .ReturnsAsync(survey);

            var dto = new SubmitResponseDto { SurveyVersionId = 1, Answers = new List<SubmitAnswerDto>() };

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _responseService.SubmitAsync(survey.PublicToken, dto, null));
            Assert.That(ex.Message, Does.Contain("has not started"));
        }

        [Test]
        public void SubmitAsync_SurveyEnded_ThrowsBadRequestException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Status = SurveyStatus.Active,
                EndDate = DateTime.UtcNow.AddDays(-1),
                PublicToken = Guid.NewGuid(),
                AllowAnonymous = true
            };
            _surveyRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Survey, bool>>>()))
                .ReturnsAsync(survey);

            var dto = new SubmitResponseDto { SurveyVersionId = 1, Answers = new List<SubmitAnswerDto>() };

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _responseService.SubmitAsync(survey.PublicToken, dto, null));
            Assert.That(ex.Message, Does.Contain("has ended"));
        }

        [Test]
        public void SubmitAsync_AnonymousNotAllowed_ThrowsForbiddenException()
        {
            // Arrange
            var survey = new Survey
            {
                Id = 1, Status = SurveyStatus.Active,
                AllowAnonymous = false,
                PublicToken = Guid.NewGuid()
            };
            _surveyRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Survey, bool>>>()))
                .ReturnsAsync(survey);

            var dto = new SubmitResponseDto { SurveyVersionId = 1, Answers = new List<SubmitAnswerDto>() };

            // Act & Assert — null userId = anonymous
            Assert.ThrowsAsync<ForbiddenException>(() => _responseService.SubmitAsync(survey.PublicToken, dto, null));
        }

        [Test]
        public void SubmitAsync_DuplicateSubmission_ThrowsConflictException()
        {
            // Arrange
            var survey = new Survey { Id = 1, Status = SurveyStatus.Active, AllowAnonymous = false, PublicToken = Guid.NewGuid() };
            _surveyRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Survey, bool>>>()))
                .ReturnsAsync(survey);

            var version = new SurveyVersion
            {
                Id = 1, SurveyId = 1, VersionNumber = 1,
                Questions = new List<Question>()
            };
            var versions = new List<SurveyVersion> { version };
            _versionRepoMock.Setup(r => r.GetQueryable()).Returns(versions.AsQueryable().BuildMock());

            _responseRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>()))
                .ReturnsAsync(true);

            var dto = new SubmitResponseDto { SurveyVersionId = 1, Answers = new List<SubmitAnswerDto>() };

            // Act & Assert
            Assert.ThrowsAsync<ConflictException>(() => _responseService.SubmitAsync(survey.PublicToken, dto, 1));
        }

        [Test]
        public void GetResponsesAsync_SurveyNotFound_ThrowsNotFoundException()
        {
            // Arrange
            _surveyRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Survey?)null);

            var pagination = new PaginationParams { PageNumber = 1, PageSize = 10 };

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _responseService.GetResponsesAsync(999, pagination, 1, "Admin"));
        }

        [Test]
        public void GetResponsesAsync_NotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var survey = new Survey { Id = 1, CreatedBy = 2 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            var pagination = new PaginationParams { PageNumber = 1, PageSize = 10 };

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _responseService.GetResponsesAsync(1, pagination, 1, "Creator"));
        }
    }
}
