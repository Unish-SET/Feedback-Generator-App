using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using FeedBackApp.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class SurveyService : ISurveyService
    {
        private readonly IRepository<Survey>         _surveyRepo;
        private readonly IRepository<Question>       _questionRepo;
        private readonly IRepository<QuestionOption> _optionRepo;
        private readonly IRepository<User>           _userRepo;
        private readonly IAuditService               _audit;

        public SurveyService(
            IRepository<Survey>         surveyRepo,
            IRepository<Question>       questionRepo,
            IRepository<QuestionOption> optionRepo,
            IRepository<User>           userRepo,
            IAuditService               audit)
        {
            _surveyRepo   = surveyRepo;
            _questionRepo = questionRepo;
            _optionRepo   = optionRepo;
            _userRepo     = userRepo;
            _audit        = audit;
        }

        // ── Create ────────────────────────────────────────────────────────────
        public async Task<SurveyResponseDto> CreateAsync(CreateSurveyDto dto, int userId)
        {
            var survey = new Survey
            {
                Title          = dto.Title,
                Description    = dto.Description,
                PublicToken    = Guid.NewGuid(),
                State          = SurveyState.Inactive,
                StartDate      = ToUtc(dto.StartDate),
                EndDate        = ToUtc(dto.EndDate),
                AllowAnonymous = dto.AllowAnonymous,
                CreatedBy      = userId,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };

            await _surveyRepo.AddAsync(survey);
            await _surveyRepo.SaveChangesAsync();

            if (dto.State?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true)
            {
                await ValidateCanActivate(survey.Id);
                survey.State     = SurveyState.Active;
                survey.UpdatedAt = DateTime.UtcNow;
                _surveyRepo.Update(survey);
                await _surveyRepo.SaveChangesAsync();
                _ = _audit.LogAsync("Publish", "Survey", survey.Id.ToString(), userId);
            }
            else
            {
                _ = _audit.LogAsync("Create", "Survey", survey.Id.ToString(), userId);
            }

            return MapToResponseDto(survey);
        }

        // ── Update ────────────────────────────────────────────────────────────
        public async Task<SurveyResponseDto> UpdateAsync(int surveyId, UpdateSurveyDto dto, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);

            if (survey.State != SurveyState.Inactive)
                throw new BadRequestException("Only Inactive surveys can be edited. Pause an Active survey first.");

            survey.Title          = dto.Title;
            survey.Description    = dto.Description;
            survey.StartDate      = ToUtc(dto.StartDate);
            survey.EndDate        = ToUtc(dto.EndDate);
            survey.AllowAnonymous = dto.AllowAnonymous;
            survey.UpdatedAt      = DateTime.UtcNow;

            _surveyRepo.Update(survey);
            await _surveyRepo.SaveChangesAsync();
            _ = _audit.LogAsync("Update", "Survey", surveyId.ToString(), userId);
            return MapToResponseDto(survey);
        }

        // ── Get by ID ─────────────────────────────────────────────────────────
        public async Task<SurveyResponseDto> GetByIdAsync(int surveyId, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);
            return MapToResponseDto(survey);
        }

        // ── Get All (paginated) ───────────────────────────────────────────────
        public async Task<PaginatedResult<SurveyListDto>> GetAllAsync(SurveyFilterParams filter, int userId, string role)
        {
            var pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            var pageSize   = filter.PageSize   <= 0 ? 20 : Math.Min(filter.PageSize, 50);

            var query = _surveyRepo.GetQueryable().AsQueryable();

            if (!RoleHelper.IsAdmin(role))
                query = query.Where(s => s.CreatedBy == userId);

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(s => s.Title.Contains(filter.Search));

            if (!string.IsNullOrWhiteSpace(filter.Status) &&
                Enum.TryParse<SurveyState>(filter.Status, true, out var stateEnum))
                query = query.Where(s => s.State == stateEnum);

            if (RoleHelper.IsAdmin(role) && filter.CreatedBy.HasValue)
                query = query.Where(s => s.CreatedBy == filter.CreatedBy.Value);

            if (filter.StartDateFrom.HasValue)
                query = query.Where(s => s.StartDate >= filter.StartDateFrom.Value.ToUniversalTime());

            if (filter.StartDateTo.HasValue)
                query = query.Where(s => s.StartDate <= filter.StartDateTo.Value.ToUniversalTime());

            var totalCount = await query.CountAsync();

            var descending = string.IsNullOrWhiteSpace(filter.SortDir) ||
                             filter.SortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

            IQueryable<Survey> sorted = filter.SortBy?.ToLower() switch
            {
                "title"         => descending ? query.OrderByDescending(s => s.Title)
                                              : query.OrderBy(s => s.Title),
                "responsecount" => descending
                                    ? query.OrderByDescending(s => s.Responses.Count)
                                    : query.OrderBy(s => s.Responses.Count),
                _               => descending ? query.OrderByDescending(s => s.CreatedAt)
                                              : query.OrderBy(s => s.CreatedAt)
            };

            var surveys = await sorted
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SurveyListDto
                {
                    Id            = s.Id,
                    Title         = s.Title,
                    State         = s.State.ToString(),
                    PublicToken   = s.PublicToken.ToString(),
                    ResponseCount = s.Responses.Count,
                    CreatedAt     = s.CreatedAt,
                    CreatedBy     = s.CreatedBy,
                    CreatorName   = s.Creator.Username
                })
                .ToListAsync();

            return new PaginatedResult<SurveyListDto>
            {
                Items      = surveys,
                PageNumber = pageNumber,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }

        // ── Set State ─────────────────────────────────────────────────────────
        public async Task<SurveyResponseDto> SetStateAsync(int surveyId, SetSurveyStateDto dto, int userId, string role)
        {
            if (!Enum.TryParse<SurveyState>(dto.State, true, out var targetState))
                throw new BadRequestException(
                    $"Invalid state '{dto.State}'. Valid values: Inactive, Active, Closed.");

            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);

            if (survey.State == targetState)
                throw new BadRequestException("Survey is already in that state.");

            switch (targetState)
            {
                case SurveyState.Active when survey.State == SurveyState.Inactive:
                case SurveyState.Active when survey.State == SurveyState.Closed:
                    await ValidateCanActivate(surveyId);
                    survey.State     = SurveyState.Active;
                    survey.UpdatedAt = DateTime.UtcNow;
                    _surveyRepo.Update(survey);
                    await _surveyRepo.SaveChangesAsync();
                    _ = _audit.LogAsync("Publish", "Survey", surveyId.ToString(), userId);
                    break;

                case SurveyState.Inactive when survey.State == SurveyState.Active:
                case SurveyState.Inactive when survey.State == SurveyState.Closed:
                    survey.State     = SurveyState.Inactive;
                    survey.UpdatedAt = DateTime.UtcNow;
                    _surveyRepo.Update(survey);
                    await _surveyRepo.SaveChangesAsync();
                    _ = _audit.LogAsync("Pause", "Survey", surveyId.ToString(), userId);
                    break;

                case SurveyState.Closed:
                    survey.State     = SurveyState.Closed;
                    survey.UpdatedAt = DateTime.UtcNow;
                    _surveyRepo.Update(survey);
                    await _surveyRepo.SaveChangesAsync();
                    _ = _audit.LogAsync("Close", "Survey", surveyId.ToString(), userId);
                    break;

                default:
                    throw new BadRequestException(
                        $"Transition from {survey.State} to {targetState} is not allowed.");
            }

            return MapToResponseDto(survey);
        }

        // ── Delete ────────────────────────────────────────────────────────────
        public async Task DeleteAsync(int surveyId, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);
            survey.IsDeleted = true;
            survey.UpdatedAt = DateTime.UtcNow;
            _surveyRepo.Update(survey);
            await _surveyRepo.SaveChangesAsync();
            _ = _audit.LogAsync("Delete", "Survey", surveyId.ToString(), userId);
        }

        // ── Public access (anyone with the link) ──────────────────────────────
        public async Task<PublicSurveyDto> GetByPublicTokenAsync(Guid publicToken)
        {
            var survey = await _surveyRepo.GetQueryable()
                .Include(s => s.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.Options.OrderBy(o => o.Order))
                .FirstOrDefaultAsync(s => s.PublicToken == publicToken);

            if (survey == null)
                throw new NotFoundException("Survey not found.");

            switch (survey.State)
            {
                case SurveyState.Inactive:
                    throw new BadRequestException("SURVEY_PAUSED");
                case SurveyState.Closed:
                    throw new BadRequestException("SURVEY_CLOSED");
            }

            var now = DateTime.UtcNow;
            if (survey.StartDate.HasValue &&
                DateTime.SpecifyKind(survey.StartDate.Value, DateTimeKind.Utc) > now)
                throw new BadRequestException("SURVEY_NOT_STARTED");

            if (survey.EndDate.HasValue &&
                DateTime.SpecifyKind(survey.EndDate.Value, DateTimeKind.Utc) < now)
                throw new BadRequestException("SURVEY_EXPIRED");

            if (!survey.Questions.Any())
                throw new BadRequestException("SURVEY_NO_QUESTIONS");

            return new PublicSurveyDto
            {
                Title          = survey.Title,
                Description    = survey.Description,
                AllowAnonymous = survey.AllowAnonymous,
                Questions      = survey.Questions
                    .OrderBy(q => q.Order)
                    .Select(q => new PublicQuestionDto
                    {
                        Id         = q.Id,
                        Text       = q.Text,
                        Type       = q.Type.ToString(),
                        IsRequired = q.IsRequired,
                        Order      = q.Order,
                        Options    = q.Options
                            .OrderBy(o => o.Order)
                            .Select(o => new PublicOptionDto
                            {
                                Id    = o.Id,
                                Text  = o.Text,
                                Order = o.Order
                            }).ToList()
                    }).ToList()
            };
        }

        // ── Clone Questions ───────────────────────────────────────────────────
        public async Task CloneQuestionsAsync(int sourceSurveyId, int targetSurveyId, int userId, string role)
        {
            await GetSurveyWithAccessCheck(sourceSurveyId, userId, role);
            var targetSurvey = await GetSurveyWithAccessCheck(targetSurveyId, userId, role);

            if (targetSurvey.State != SurveyState.Inactive)
                throw new BadRequestException("Questions can only be cloned into an Inactive survey.");

            var sourceQuestions = await _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .Where(q => q.SurveyId == sourceSurveyId)
                .OrderBy(q => q.Order)
                .ToListAsync();

            if (!sourceQuestions.Any())
                throw new BadRequestException("Source survey has no questions to clone.");

            var maxOrder = await _questionRepo.GetQueryable()
                .Where(q => q.SurveyId == targetSurveyId)
                .Select(q => (int?)q.Order)
                .MaxAsync() ?? 0;

            foreach (var sourceQuestion in sourceQuestions)
            {
                maxOrder++;
                var newQuestion = new Question
                {
                    SurveyId   = targetSurveyId,
                    Text       = sourceQuestion.Text,
                    Type       = sourceQuestion.Type,
                    IsRequired = sourceQuestion.IsRequired,
                    Order      = maxOrder,
                    Options    = sourceQuestion.Options
                        .OrderBy(o => o.Order)
                        .Select(o => new QuestionOption { Text = o.Text, Order = o.Order })
                        .ToList()
                };
                await _questionRepo.AddAsync(newQuestion);
            }

            await _questionRepo.SaveChangesAsync();
        }

        // ── Private Helpers ───────────────────────────────────────────────────
        private async Task ValidateCanActivate(int surveyId)
        {
            var hasQuestions = await _questionRepo.GetQueryable()
                .AnyAsync(q => q.SurveyId == surveyId && !string.IsNullOrWhiteSpace(q.Text));

            if (!hasQuestions)
                throw new BadRequestException(
                    "Cannot publish a survey with no questions. Add at least one question first.");
        }

        private static DateTime? ToUtc(DateTime? dt)
        {
            if (!dt.HasValue) return null;
            return dt.Value.Kind == DateTimeKind.Utc
                ? dt.Value
                : DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc);
        }

        private async Task<Survey> GetSurveyWithAccessCheck(int surveyId, int userId, string role)
        {
            var survey = await _surveyRepo.GetQueryable()
                .Include(s => s.Creator)
                .FirstOrDefaultAsync(s => s.Id == surveyId && !s.IsDeleted);

            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey.");

            return survey;
        }

        private static SurveyResponseDto MapToResponseDto(Survey survey)
        {
            return new SurveyResponseDto
            {
                Id             = survey.Id,
                Title          = survey.Title,
                Description    = survey.Description,
                PublicToken    = survey.PublicToken.ToString(),
                State          = survey.State.ToString(),
                StartDate      = survey.StartDate,
                EndDate        = survey.EndDate,
                AllowAnonymous = survey.AllowAnonymous,
                CreatedBy      = survey.CreatedBy,
                CreatorName    = survey.Creator?.Username ?? "Unknown",
                CreatedAt      = survey.CreatedAt,
                UpdatedAt      = survey.UpdatedAt
            };
        }
    }
}
