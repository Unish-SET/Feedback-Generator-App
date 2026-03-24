using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class SurveyService : ISurveyService
    {
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IRepository<SurveyVersion> _versionRepo;
        private readonly IRepository<Question> _questionRepo;
        private readonly IRepository<QuestionOption> _optionRepo;
        private readonly IRepository<User> _userRepo;

        public SurveyService(
            IRepository<Survey> surveyRepo,
            IRepository<SurveyVersion> versionRepo,
            IRepository<Question> questionRepo,
            IRepository<QuestionOption> optionRepo,
            IRepository<User> userRepo)
        {
            _surveyRepo = surveyRepo;
            _versionRepo = versionRepo;
            _questionRepo = questionRepo;
            _optionRepo = optionRepo;
            _userRepo = userRepo;
        }

        public async Task<SurveyResponseDto> CreateAsync(CreateSurveyDto dto, int userId)
        {
            var survey = new Survey
            {
                Title = dto.Title,
                Description = dto.Description,
                PublicToken = Guid.NewGuid(),
                Status = SurveyStatus.Draft,
                StartDate = ToUtc(dto.StartDate),
                EndDate = ToUtc(dto.EndDate),
                AllowAnonymous = dto.AllowAnonymous,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _surveyRepo.AddAsync(survey);
            await _surveyRepo.SaveChangesAsync();

            var version = new SurveyVersion
            {
                SurveyId = survey.Id,
                VersionNumber = 1,
                CreatedAt = DateTime.UtcNow
            };

            await _versionRepo.AddAsync(version);
            await _versionRepo.SaveChangesAsync();

            return await MapToResponseDto(survey);
        }

        public async Task<SurveyResponseDto> UpdateAsync(int surveyId, UpdateSurveyDto dto, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);

            if (survey.Status != SurveyStatus.Draft)
                throw new BadRequestException("Only Draft surveys can be edited.");

            survey.Title = dto.Title;
            survey.Description = dto.Description;
            survey.StartDate = ToUtc(dto.StartDate);
            survey.EndDate = ToUtc(dto.EndDate);
            survey.AllowAnonymous = dto.AllowAnonymous;
            survey.UpdatedAt = DateTime.UtcNow;

            _surveyRepo.Update(survey);
            await _surveyRepo.SaveChangesAsync();
            return await MapToResponseDto(survey);
        }

        public async Task<SurveyResponseDto> GetByIdAsync(int surveyId, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);
            return await MapToResponseDto(survey);
        }

        public async Task<PaginatedResult<SurveyListDto>> GetAllAsync(PaginationParams pagination, int userId, string role)
        {
            var pageNumber = pagination.PageNumber <= 0 ? 1 : pagination.PageNumber;
            var pageSize = pagination.PageSize <= 0 ? 20 : Math.Min(pagination.PageSize, 50);

            var query = _surveyRepo.GetQueryable()
                .Include(s => s.Versions)
                .Join(_userRepo.GetQueryable(),
                    s => s.CreatedBy,
                    u => u.Id,
                    (s, u) => new { Survey = s, CreatorName = u.Username })
                .Where(x => !x.Survey.IsDeleted)
                .AsQueryable();

            if (role != UserRole.Admin.ToString())
            {
                query = query.Where(x => x.Survey.CreatedBy == userId);
            }

            var totalCount = await query.CountAsync();

            var surveys = await query
                .OrderByDescending(x => x.Survey.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new SurveyListDto
                {
                    Id = x.Survey.Id,
                    Title = x.Survey.Title,
                    Status = x.Survey.Status.ToString(),
                    PublicToken = x.Survey.PublicToken.ToString(),
                    ResponseCount = x.Survey.Versions.SelectMany(v => v.Responses).Count(),
                    CurrentVersion = x.Survey.Versions
                        .OrderByDescending(v => v.VersionNumber)
                        .Select(v => v.VersionNumber)
                        .FirstOrDefault(),
                    CreatedAt = x.Survey.CreatedAt,
                    CreatedBy = x.Survey.CreatedBy,
                    CreatorName = x.CreatorName
                })
                .ToListAsync();

            return new PaginatedResult<SurveyListDto>
            {
                Items = surveys,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<SurveyResponseDto> PublishAsync(int surveyId, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);

            if (survey.Status == SurveyStatus.Active)
                throw new BadRequestException("Survey is already active.");

            if (survey.Status == SurveyStatus.Closed)
                throw new BadRequestException("Closed surveys cannot be published. Create a new survey instead.");

            survey.Status = SurveyStatus.Active;
            survey.UpdatedAt = DateTime.UtcNow;
            _surveyRepo.Update(survey);
            await _surveyRepo.SaveChangesAsync();

            return await MapToResponseDto(survey);
        }

        public async Task<SurveyResponseDto> UnpublishAsync(int surveyId, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);

            if (survey.Status != SurveyStatus.Active)
                throw new BadRequestException("Only Active surveys can be unpublished.");

            survey.Status = SurveyStatus.Draft;
            survey.UpdatedAt = DateTime.UtcNow;
            _surveyRepo.Update(survey);
            await _surveyRepo.SaveChangesAsync();

            return await MapToResponseDto(survey);
        }

        public async Task<SurveyResponseDto> CloseAsync(int surveyId, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);

            if (survey.Status == SurveyStatus.Closed)
                throw new BadRequestException("Survey is already closed.");

            survey.Status = SurveyStatus.Closed;
            survey.UpdatedAt = DateTime.UtcNow;
            _surveyRepo.Update(survey);
            await _surveyRepo.SaveChangesAsync();

            return await MapToResponseDto(survey);
        }

        public async Task DeleteAsync(int surveyId, int userId, string role)
        {
            var survey = await GetSurveyWithAccessCheck(surveyId, userId, role);
            survey.IsDeleted = true;
            survey.UpdatedAt = DateTime.UtcNow;
            _surveyRepo.Update(survey);
            await _surveyRepo.SaveChangesAsync();
        }

        public async Task<PublicSurveyDto> GetByPublicTokenAsync(Guid publicToken)
        {
            var survey = await _surveyRepo.GetQueryable()
                .Include(s => s.Versions)
                    .ThenInclude(v => v.Questions.OrderBy(q => q.Order))
                        .ThenInclude(q => q.Options.OrderBy(o => o.Order))
                .FirstOrDefaultAsync(s => s.PublicToken == publicToken);

            if (survey == null)
                throw new NotFoundException("Survey not found.");

            if (survey.Status == SurveyStatus.Draft)
                throw new BadRequestException("SURVEY_NOT_PUBLISHED");

            if (survey.Status == SurveyStatus.Closed)
                throw new BadRequestException("SURVEY_CLOSED");

            var now = DateTime.UtcNow;

            // Dates are stored as UTC (normalized via ToUtc on save).
            // SpecifyKind guarantees the comparison is UTC even if EF returns Unspecified kind.
            if (survey.StartDate.HasValue && DateTime.SpecifyKind(survey.StartDate.Value, DateTimeKind.Utc) > now)
                throw new BadRequestException("SURVEY_NOT_STARTED");

            if (survey.EndDate.HasValue && DateTime.SpecifyKind(survey.EndDate.Value, DateTimeKind.Utc) < now)
                throw new BadRequestException("SURVEY_EXPIRED");

            var latestVersion = survey.Versions
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();

            if (latestVersion == null)
                throw new BadRequestException("SURVEY_NO_QUESTIONS");

            return new PublicSurveyDto
            {
                Title = survey.Title,
                Description = survey.Description,
                AllowAnonymous = survey.AllowAnonymous,
                VersionId = latestVersion.Id,
                Questions = latestVersion.Questions
                    .OrderBy(q => q.Order)
                    .Select(q => new PublicQuestionDto
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Type = q.Type.ToString(),
                        IsRequired = q.IsRequired,
                        Order = q.Order,
                        Options = q.Options
                            .OrderBy(o => o.Order)
                            .Select(o => new PublicOptionDto
                            {
                                Id = o.Id,
                                Text = o.Text,
                                Order = o.Order
                            }).ToList()
                    }).ToList()
            };
        }

        public async Task CloneQuestionsAsync(int sourceSurveyId, int targetSurveyId, int userId, string role)
        {
            await GetSurveyWithAccessCheck(sourceSurveyId, userId, role);
            var targetSurvey = await GetSurveyWithAccessCheck(targetSurveyId, userId, role);

            if (targetSurvey.Status != SurveyStatus.Draft)
                throw new BadRequestException("Questions can only be cloned into a Draft survey.");

            var sourceVersion = await _versionRepo.GetQueryable()
                .Include(v => v.Questions)
                    .ThenInclude(q => q.Options)
                .Where(v => v.SurveyId == sourceSurveyId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            if (sourceVersion == null || !sourceVersion.Questions.Any())
                throw new BadRequestException("Source survey has no questions to clone.");

            var targetVersion = await _versionRepo.GetQueryable()
                .Include(v => v.Questions)
                .Where(v => v.SurveyId == targetSurveyId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            if (targetVersion == null)
                throw new BadRequestException("Target survey has no version. Please save the survey first.");

            var maxOrder = targetVersion.Questions.Any()
                ? targetVersion.Questions.Max(q => q.Order)
                : 0;

            foreach (var sourceQuestion in sourceVersion.Questions.OrderBy(q => q.Order))
            {
                maxOrder++;
                var newQuestion = new Question
                {
                    SurveyVersionId = targetVersion.Id,
                    Text = sourceQuestion.Text,
                    Type = sourceQuestion.Type,
                    IsRequired = sourceQuestion.IsRequired,
                    Order = maxOrder
                };

                await _questionRepo.AddAsync(newQuestion);
                await _questionRepo.SaveChangesAsync();

                foreach (var sourceOption in sourceQuestion.Options.OrderBy(o => o.Order))
                {
                    var newOption = new QuestionOption
                    {
                        QuestionId = newQuestion.Id,
                        Text = sourceOption.Text,
                        Order = sourceOption.Order
                    };
                    await _optionRepo.AddAsync(newOption);
                }
            }

            await _optionRepo.SaveChangesAsync();
        }

        // ── Helper Methods ──

        /// <summary>
        /// Normalizes an incoming DateTime to UTC.
        /// When the frontend sends a UTC ISO string (e.g. 2025-03-18T05:00:00Z),
        /// ASP.NET parses it correctly as UTC. This method handles the edge case where
        /// the kind is Unspecified (e.g. no 'Z' suffix) by treating it as UTC explicitly.
        /// </summary>
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
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == surveyId && !s.IsDeleted);

            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (role != UserRole.Admin.ToString() && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey.");

            return survey;
        }

        private async Task<SurveyResponseDto> MapToResponseDto(Survey survey)
        {
            User? creator = survey.Creator;
            if (creator == null && survey.CreatedBy > 0)
            {
                creator = await _userRepo.GetByIdAsync(survey.CreatedBy);
            }

            var latestVersion = await _versionRepo.GetQueryable()
                .Where(v => v.SurveyId == survey.Id)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            return new SurveyResponseDto
            {
                Id = survey.Id,
                Title = survey.Title,
                Description = survey.Description,
                PublicToken = survey.PublicToken.ToString(),
                Status = survey.Status.ToString(),
                StartDate = survey.StartDate,
                EndDate = survey.EndDate,
                AllowAnonymous = survey.AllowAnonymous,
                CreatedBy = survey.CreatedBy,
                CreatorName = creator?.Username ?? "Unknown",
                CurrentVersion = latestVersion?.VersionNumber ?? 1,
                CreatedAt = survey.CreatedAt,
                UpdatedAt = survey.UpdatedAt
            };
        }
    }
}
