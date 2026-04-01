using MockQueryable.Moq;
using Moq;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Services;

namespace FeedBackApp.Tests.Services
{
    [TestFixture]
    public class ResponseServiceTests
    {
        private Mock<IRepository<Survey>>         _surveyRepo;
        private Mock<IRepository<Question>>       _questionRepo;
        private Mock<IRepository<SurveyResponse>> _responseRepo;
        private ResponseService                   _sut;

        [SetUp]
        public void Setup()
        {
            _surveyRepo   = new Mock<IRepository<Survey>>();
            _questionRepo = new Mock<IRepository<Question>>();
            _responseRepo = new Mock<IRepository<SurveyResponse>>();

            _sut = new ResponseService(
                _surveyRepo.Object,
                _questionRepo.Object,
                _responseRepo.Object);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Survey MakeActiveSurvey(Guid? token = null, bool allowAnon = true) =>
            new Survey
            {
                Id             = 1,
                State          = SurveyState.Active,
                PublicToken    = token ?? Guid.NewGuid(),
                AllowAnonymous = allowAnon,
                Questions      = new List<Question>()
            };

        private static SubmitResponseDto EmptyDto() =>
            new SubmitResponseDto { Answers = new List<SubmitAnswerDto>() };

        // ── SubmitAsync — survey validation ───────────────────────────────────

        [Test]
        public void SubmitAsync_ShouldThrowNotFound_WhenSurveyTokenDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey>().AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.SubmitAsync(Guid.NewGuid(), EmptyDto(), null));
        }

        [Test]
        public void SubmitAsync_ShouldThrowBadRequest_WhenSurveyIsInactive()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = new Survey { Id = 1, State = SurveyState.Inactive, PublicToken = token, Questions = new List<Question>() };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.SubmitAsync(token, EmptyDto(), null));
            Assert.That(ex.Message, Does.Contain("not currently accepting"));
        }

        [Test]
        public void SubmitAsync_ShouldThrowBadRequest_WhenSurveyIsClosed()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = new Survey { Id = 1, State = SurveyState.Closed, PublicToken = token, Questions = new List<Question>() };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() => _sut.SubmitAsync(token, EmptyDto(), null));
        }

        [Test]
        public void SubmitAsync_ShouldThrowBadRequest_WhenSurveyHasNotStartedYet()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = MakeActiveSurvey(token);
            survey.StartDate = DateTime.UtcNow.AddDays(3);
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.SubmitAsync(token, EmptyDto(), null));
            Assert.That(ex.Message, Does.Contain("has not started"));
        }

        [Test]
        public void SubmitAsync_ShouldThrowBadRequest_WhenSurveyHasEnded()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = MakeActiveSurvey(token);
            survey.EndDate = DateTime.UtcNow.AddDays(-1);
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.SubmitAsync(token, EmptyDto(), null));
            Assert.That(ex.Message, Does.Contain("has ended"));
        }

        // ── SubmitAsync — access control ──────────────────────────────────────

        [Test]
        public void SubmitAsync_ShouldThrowForbidden_WhenAnonymousAndSurveyRequiresLogin()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = MakeActiveSurvey(token, allowAnon: false);
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());

            // Act & Assert — null userId = anonymous
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.SubmitAsync(token, EmptyDto(), userId: null));
        }

        [Test]
        public void SubmitAsync_ShouldThrowConflict_WhenAuthenticatedUserSubmitsTwice()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = MakeActiveSurvey(token, allowAnon: false);
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());
            _responseRepo.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>()))
                .ReturnsAsync(true);

            // Act & Assert
            Assert.ThrowsAsync<ConflictException>(() => _sut.SubmitAsync(token, EmptyDto(), userId: 1));
        }

        // ── SubmitAsync — required questions ──────────────────────────────────

        [Test]
        public void SubmitAsync_ShouldThrowBadRequest_WhenRequiredQuestionIsNotAnswered()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = MakeActiveSurvey(token);
            survey.Questions = new List<Question>
            {
                new Question { Id = 10, IsRequired = true, Options = new List<QuestionOption>() }
            };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());
            _responseRepo.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>()))
                .ReturnsAsync(false);

            var dto = new SubmitResponseDto { Answers = new List<SubmitAnswerDto>() }; // no answer for Q10

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.SubmitAsync(token, dto, userId: null));
            Assert.That(ex.Message, Does.Contain("Required questions not answered"));
        }

        // ── SubmitAsync — option validation ───────────────────────────────────

        [Test]
        public void SubmitAsync_ShouldThrowBadRequest_WhenSelectedOptionDoesNotBelongToQuestion()
        {
            // Arrange
            var token  = Guid.NewGuid();
            var survey = MakeActiveSurvey(token);
            survey.Questions = new List<Question>
            {
                new Question
                {
                    Id = 1, IsRequired = false,
                    Options = new List<QuestionOption> { new QuestionOption { Id = 10 } }
                }
            };
            _surveyRepo.Setup(r => r.GetQueryable())
                .Returns(new List<Survey> { survey }.AsQueryable().BuildMock());
            _responseRepo.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>()))
                .ReturnsAsync(false);

            // Answer references option 999 which does NOT belong to question 1
            var dto = new SubmitResponseDto
            {
                Answers = new List<SubmitAnswerDto>
                {
                    new SubmitAnswerDto { QuestionId = 1, SelectedOptionId = 999 }
                }
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<BadRequestException>(() => _sut.SubmitAsync(token, dto, userId: null));
            Assert.That(ex.Message, Does.Contain("does not belong to question"));
        }

        // ── GetResponsesAsync ─────────────────────────────────────────────────

        [Test]
        public void GetResponsesAsync_ShouldThrowNotFound_WhenSurveyDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Survey?)null);

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.GetResponsesAsync(999, new ResponseFilterParams(), userId: 1, role: "Admin"));
        }

        [Test]
        public void GetResponsesAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            var survey = new Survey { Id = 1, CreatedBy = 99 };
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.GetResponsesAsync(1, new ResponseFilterParams(), userId: 1, role: "Creator"));
        }

        [Test]
        public void GetResponsesAsync_ShouldThrowBadRequest_WhenFromDateIsAfterToDate()
        {
            // Arrange
            var survey = new Survey { Id = 1, CreatedBy = 1 };
            _surveyRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(survey);
            var filter = new ResponseFilterParams
            {
                SubmittedFrom = DateTime.UtcNow.AddDays(5),
                SubmittedTo   = DateTime.UtcNow
            };

            // Act & Assert
            Assert.ThrowsAsync<BadRequestException>(() =>
                _sut.GetResponsesAsync(1, filter, userId: 1, role: "Creator"));
        }
    }
}
