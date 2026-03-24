using MockQueryable.Moq;
using Moq;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using FeedBackApp.Services;

namespace FeedBackApp.Tests
{
    [TestFixture]
    public class AnalyticsServiceTests
    {
        private Mock<IRepository<Survey>> _surveyRepoMock;
        private Mock<IRepository<SurveyVersion>> _versionRepoMock;
        private Mock<IRepository<SurveyResponse>> _responseRepoMock;
        private Mock<IRepository<Question>> _questionRepoMock;
        private AnalyticsService _analyticsService;

        [SetUp]
        public void Setup()
        {
            _surveyRepoMock = new Mock<IRepository<Survey>>();
            _versionRepoMock = new Mock<IRepository<SurveyVersion>>();
            _responseRepoMock = new Mock<IRepository<SurveyResponse>>();
            _questionRepoMock = new Mock<IRepository<Question>>();

            _analyticsService = new AnalyticsService(
                _surveyRepoMock.Object,
                _versionRepoMock.Object,
                _responseRepoMock.Object,
                _questionRepoMock.Object);
        }

        [Test]
        public void GetAnalyticsAsync_SurveyNotFound_ThrowsNotFoundException()
        {
            // Arrange
            _surveyRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Survey?)null);

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _analyticsService.GetAnalyticsAsync(999, 1, "Admin"));
        }

        [Test]
        public void GetAnalyticsAsync_NotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var survey = new Survey { Id = 1, CreatedBy = 2 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _analyticsService.GetAnalyticsAsync(1, 1, "Creator"));
        }

        [Test]
        public void GetAnalyticsAsync_AdminCanAccessAnySurvey_NoVersionThrowsBadRequest()
        {
            // Arrange — Admin should not get ForbiddenException even if not owner
            var survey = new Survey { Id = 1, CreatedBy = 2 };
            _surveyRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            // Return empty versions to trigger "no version" error
            var emptyVersions = new List<SurveyVersion>();
            _versionRepoMock.Setup(r => r.GetQueryable()).Returns(emptyVersions.AsQueryable().BuildMock());

            // Act & Assert — should pass ownership check but throw BadRequest for no version
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _analyticsService.GetAnalyticsAsync(1, 1, "Admin"));
            Assert.That(ex.Message, Is.EqualTo("Survey has no version."));
        }
    }
}
