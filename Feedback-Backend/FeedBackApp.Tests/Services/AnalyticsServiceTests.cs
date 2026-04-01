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
    public class AnalyticsServiceTests
    {
        private Mock<IRepository<Survey>>         _surveyRepo;
        private Mock<IRepository<SurveyResponse>> _responseRepo;
        private Mock<IRepository<Question>>       _questionRepo;
        private AnalyticsService                  _sut;

        [SetUp]
        public void Setup()
        {
            _surveyRepo   = new Mock<IRepository<Survey>>();
            _responseRepo = new Mock<IRepository<SurveyResponse>>();
            _questionRepo = new Mock<IRepository<Question>>();

            _sut = new AnalyticsService(
                _surveyRepo.Object,
                _responseRepo.Object,
                _questionRepo.Object);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetupSurvey(Survey survey) =>
            _surveyRepo.Setup(r => r.GetByIdAsync(survey.Id)).ReturnsAsync(survey);

        private void SetupResponses(params SurveyResponse[] responses) =>
            _responseRepo.Setup(r => r.GetQueryable())
                .Returns(responses.ToList().AsQueryable().BuildMock());

        private void SetupQuestions(params Question[] questions) =>
            _questionRepo.Setup(r => r.GetQueryable())
                .Returns(questions.ToList().AsQueryable().BuildMock());

        // ── Access control ────────────────────────────────────────────────────

        [Test]
        public void GetAnalyticsAsync_ShouldThrowNotFound_WhenSurveyDoesNotExist()
        {
            // Arrange
            _surveyRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Survey?)null);

            // Act & Assert
            Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAnalyticsAsync(999, userId: 1, role: "Admin"));
        }

        [Test]
        public void GetAnalyticsAsync_ShouldThrowForbidden_WhenCreatorDoesNotOwnSurvey()
        {
            // Arrange
            SetupSurvey(new Survey { Id = 1, CreatedBy = 99 });

            // Act & Assert
            Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator"));
        }

        [Test]
        public async Task GetAnalyticsAsync_ShouldNotThrowForbidden_WhenAdminAccessesAnySurvey()
        {
            // Arrange — Admin can access surveys they don't own
            SetupSurvey(new Survey { Id = 1, CreatedBy = 99, Title = "T" });
            SetupResponses();
            SetupQuestions();

            // Act & Assert — no exception
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Admin");
            Assert.That(result, Is.Not.Null);
        }

        // ── TotalResponses ────────────────────────────────────────────────────

        [Test]
        public async Task GetAnalyticsAsync_ShouldReturnZeroTotalResponses_WhenNoResponsesExist()
        {
            // Arrange
            SetupSurvey(new Survey { Id = 1, CreatedBy = 1, Title = "T" });
            SetupResponses();
            SetupQuestions();

            // Act
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator");

            // Assert
            Assert.That(result.TotalResponses, Is.EqualTo(0));
        }

        [Test]
        public async Task GetAnalyticsAsync_ShouldReturnCorrectTotalResponses_WhenResponsesExist()
        {
            // Arrange
            SetupSurvey(new Survey { Id = 1, CreatedBy = 1, Title = "T" });
            SetupResponses(
                new SurveyResponse { Id = 1, SurveyId = 1, SubmittedAt = DateTime.UtcNow },
                new SurveyResponse { Id = 2, SurveyId = 1, SubmittedAt = DateTime.UtcNow },
                new SurveyResponse { Id = 3, SurveyId = 1, SubmittedAt = DateTime.UtcNow }
            );
            SetupQuestions();

            // Act
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator");

            // Assert
            Assert.That(result.TotalResponses, Is.EqualTo(3));
        }

        // ── Date filter ───────────────────────────────────────────────────────

        [Test]
        public async Task GetAnalyticsAsync_ShouldFilterByFromDate_WhenFilterProvided()
        {
            // Arrange
            var cutoff = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            SetupSurvey(new Survey { Id = 1, CreatedBy = 1, Title = "T" });
            SetupResponses(
                new SurveyResponse { Id = 1, SurveyId = 1, SubmittedAt = cutoff.AddDays(-1) }, // before
                new SurveyResponse { Id = 2, SurveyId = 1, SubmittedAt = cutoff.AddDays(1) }   // after
            );
            SetupQuestions();

            var filter = new AnalyticsFilterParams { FromDate = cutoff };

            // Act
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator", filter: filter);

            // Assert — only the response after cutoff is counted
            Assert.That(result.TotalResponses, Is.EqualTo(1));
        }

        // ── Rating analytics ──────────────────────────────────────────────────

        [Test]
        public async Task GetAnalyticsAsync_ShouldCalculateAverageRating_ForRatingScaleQuestion()
        {
            // Arrange
            SetupSurvey(new Survey { Id = 1, CreatedBy = 1, Title = "T" });
            SetupResponses(
                new SurveyResponse { Id = 1, SurveyId = 1, SubmittedAt = DateTime.UtcNow }
            );
            var question = new Question
            {
                Id = 1, SurveyId = 1, Type = QuestionType.RatingScale, Order = 1,
                Options = new List<QuestionOption>(),
                Answers = new List<Answer>
                {
                    new Answer { RatingValue = 4 },
                    new Answer { RatingValue = 2 }
                }
            };
            SetupQuestions(question);

            // Act
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator");

            // Assert
            var qa = result.Questions.First();
            Assert.That(qa.AverageRating, Is.EqualTo(3.0));
        }

        [Test]
        public async Task GetAnalyticsAsync_ShouldReturnNullAverageRating_WhenNoRatingAnswersExist()
        {
            // Arrange
            SetupSurvey(new Survey { Id = 1, CreatedBy = 1, Title = "T" });
            SetupResponses();
            var question = new Question
            {
                Id = 1, SurveyId = 1, Type = QuestionType.RatingScale, Order = 1,
                Options = new List<QuestionOption>(),
                Answers = new List<Answer>()
            };
            SetupQuestions(question);

            // Act
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator");

            // Assert
            Assert.That(result.Questions.First().AverageRating, Is.Null);
        }

        // ── SingleChoice analytics ────────────────────────────────────────────

        [Test]
        public async Task GetAnalyticsAsync_ShouldCalculateOptionDistribution_ForSingleChoiceQuestion()
        {
            // Arrange
            SetupSurvey(new Survey { Id = 1, CreatedBy = 1, Title = "T" });
            SetupResponses(
                new SurveyResponse { Id = 1, SurveyId = 1, SubmittedAt = DateTime.UtcNow }
            );
            var opt1 = new QuestionOption { Id = 10, Text = "Yes", Order = 1 };
            var opt2 = new QuestionOption { Id = 11, Text = "No",  Order = 2 };
            var question = new Question
            {
                Id = 1, SurveyId = 1, Type = QuestionType.SingleChoice, Order = 1,
                Options = new List<QuestionOption> { opt1, opt2 },
                Answers = new List<Answer>
                {
                    new Answer { SelectedOptionId = 10 },
                    new Answer { SelectedOptionId = 10 },
                    new Answer { SelectedOptionId = 11 }
                }
            };
            SetupQuestions(question);

            // Act
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator");

            // Assert
            var dist = result.Questions.First().OptionDistributions;
            var yesDist = dist.First(d => d.OptionId == 10);
            var noDist  = dist.First(d => d.OptionId == 11);

            Assert.That(yesDist.Count,      Is.EqualTo(2));
            Assert.That(noDist.Count,       Is.EqualTo(1));
            Assert.That(yesDist.Percentage, Is.EqualTo(66.67).Within(0.01));
            Assert.That(noDist.Percentage,  Is.EqualTo(33.33).Within(0.01));
        }

        // ── DateWiseCounts ────────────────────────────────────────────────────

        [Test]
        public async Task GetAnalyticsAsync_ShouldGroupResponsesByDate_ForDateWiseCounts()
        {
            // Arrange
            SetupSurvey(new Survey { Id = 1, CreatedBy = 1, Title = "T" });
            var day1 = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            var day2 = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);
            SetupResponses(
                new SurveyResponse { Id = 1, SurveyId = 1, SubmittedAt = day1 },
                new SurveyResponse { Id = 2, SurveyId = 1, SubmittedAt = day1 },
                new SurveyResponse { Id = 3, SurveyId = 1, SubmittedAt = day2 }
            );
            SetupQuestions();

            // Act
            var result = await _sut.GetAnalyticsAsync(1, userId: 1, role: "Creator");

            // Assert
            Assert.That(result.DateWiseCounts.Count, Is.EqualTo(2));
            Assert.That(result.DateWiseCounts.First(d => d.Date == "2026-03-01").Count, Is.EqualTo(2));
            Assert.That(result.DateWiseCounts.First(d => d.Date == "2026-03-02").Count, Is.EqualTo(1));
        }
    }
}
