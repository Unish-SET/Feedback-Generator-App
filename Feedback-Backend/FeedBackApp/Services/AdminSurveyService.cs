using FeedBackApp.Context;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class AdminSurveyService : IAdminSurveyService
    {
        private readonly FeedBackDbContext _db;
        private readonly IAuditService    _audit;

        public AdminSurveyService(FeedBackDbContext db, IAuditService audit)
        {
            _db    = db;
            _audit = audit;
        }

        public async Task<PaginatedResult<AdminSurveyListDto>> GetAllSurveysAsync(AdminSurveyFilterParams filter)
        {
            var page     = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            var pageSize = Math.Min(filter.PageSize <= 0 ? 20 : filter.PageSize, 50);

            // IgnoreQueryFilters so we can see soft-deleted surveys too
            var query = _db.Surveys
                .IgnoreQueryFilters()
                .AsQueryable();

            // ── Filters ───────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(s => s.Title.Contains(filter.Search));

            if (filter.IsDeleted.HasValue)
                query = query.Where(s => s.IsDeleted == filter.IsDeleted.Value);

            if (filter.FromDate.HasValue)
                query = query.Where(s => s.CreatedAt >= filter.FromDate.Value.ToUniversalTime());

            if (filter.ToDate.HasValue)
                query = query.Where(s => s.CreatedAt <= filter.ToDate.Value.ToUniversalTime());

            var totalCount = await query.CountAsync();

            // ── Projection — no Include, no N+1 ──────────────────────────────
            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new AdminSurveyListDto
                {
                    Id            = s.Id,
                    Title         = s.Title,
                    Status        = s.State.ToString(),
                    CreatedBy     = s.Creator.Username,
                    CreatorEmail  = s.Creator.Email,
                    CreatedAt     = s.CreatedAt,
                    IsDeleted     = s.IsDeleted,
                    // Subquery count — translated to a single SQL COUNT per row
                    ResponseCount = s.Responses.Count()
                })
                .ToListAsync();

            return new PaginatedResult<AdminSurveyListDto>
            {
                Items      = items,
                PageNumber = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<AdminSurveyDetailDto> GetSurveyDetailAsync(int surveyId)
        {
            var survey = await _db.Surveys
                .IgnoreQueryFilters()
                .Include(s => s.Creator)
                .Include(s => s.Questions)
                .Include(s => s.Responses)
                .FirstOrDefaultAsync(s => s.Id == surveyId);

            if (survey == null)
                throw new NotFoundException($"Survey {surveyId} not found.");

            return new AdminSurveyDetailDto
            {
                Id             = survey.Id,
                Title          = survey.Title,
                Description    = survey.Description,
                Status         = survey.State.ToString(),
                CreatedBy      = survey.Creator.Username,
                CreatorEmail   = survey.Creator.Email,
                CreatedAt      = survey.CreatedAt,
                UpdatedAt      = survey.UpdatedAt,
                IsDeleted      = survey.IsDeleted,
                AllowAnonymous = survey.AllowAnonymous,
                StartDate      = survey.StartDate,
                EndDate        = survey.EndDate,
                TotalResponses = survey.Responses.Count,
                QuestionCount  = survey.Questions.Count
            };
        }

        public async Task SetSurveyStatusAsync(int surveyId, string status, int adminId)
        {
            if (!Enum.TryParse<Models.Enums.SurveyState>(status, true, out var newState))
                throw new BadRequestException($"Invalid state '{status}'. Valid values: Inactive, Active, Closed.");

            var survey = await _db.Surveys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == surveyId);

            if (survey == null)
                throw new NotFoundException($"Survey {surveyId} not found.");

            if (survey.IsDeleted)
                throw new BadRequestException("Cannot change state of a deleted survey.");

            if (survey.State == newState)
                throw new BadRequestException($"Survey is already {newState}.");

            // Closed surveys can be reopened (set back to Inactive or Active).

            // Prevent activating a survey with no questions
            if (newState == Models.Enums.SurveyState.Active)
            {
                var hasQuestions = await _db.Questions
                    .IgnoreQueryFilters()
                    .AnyAsync(q => q.SurveyId == surveyId && !string.IsNullOrWhiteSpace(q.Text));

                if (!hasQuestions)
                    throw new BadRequestException(
                        "Cannot activate a survey with no questions. Add at least one question first.");
            }

            survey.State     = newState;
            survey.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _ = _audit.LogAsync(
                action:     $"SetState:{newState}",
                entityName: "Survey",
                entityId:   surveyId.ToString(),
                userId:     adminId);
        }

        public async Task SoftDeleteSurveyAsync(int surveyId, int adminId, string? ip, string? correlationId)
        {
            var survey = await _db.Surveys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == surveyId);

            if (survey == null)
                throw new NotFoundException($"Survey {surveyId} not found.");

            if (survey.IsDeleted)
                throw new BadRequestException("Survey is already deleted.");

            survey.IsDeleted  = true;
            survey.UpdatedAt  = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Fire-and-forget audit — never crashes the caller
            _ = _audit.LogAsync(
                action:        "DELETE_SURVEY",
                entityName:    "Survey",
                entityId:      surveyId.ToString(),
                userId:        adminId,
                ipAddress:     ip,
                correlationId: correlationId);
        }

        public async Task RestoreSurveyAsync(int surveyId, int adminId, string? ip, string? correlationId)
        {
            var survey = await _db.Surveys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == surveyId);

            if (survey == null)
                throw new NotFoundException($"Survey {surveyId} not found.");

            if (!survey.IsDeleted)
                throw new BadRequestException("Survey is not deleted.");

            survey.IsDeleted = false;
            survey.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _ = _audit.LogAsync(
                action:        "RESTORE_SURVEY",
                entityName:    "Survey",
                entityId:      surveyId.ToString(),
                userId:        adminId,
                ipAddress:     ip,
                correlationId: correlationId);
        }

        public async Task<AdminStatsDto> GetStatsAsync()
        {
            // Sequential — EF Core does NOT allow parallel queries on the same DbContext
            var surveyStats = await _db.Surveys
                .IgnoreQueryFilters()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total   = g.Count(),
                    Active  = g.Count(s => !s.IsDeleted && s.State == Models.Enums.SurveyState.Active),
                    Deleted = g.Count(s => s.IsDeleted)
                })
                .FirstOrDefaultAsync();

            var totalResponses = await _db.SurveyResponses
                .IgnoreQueryFilters()
                .CountAsync();

            return new AdminStatsDto
            {
                TotalSurveys   = surveyStats?.Total   ?? 0,
                ActiveSurveys  = surveyStats?.Active  ?? 0,
                DeletedSurveys = surveyStats?.Deleted ?? 0,
                TotalResponses = totalResponses
            };
        }
    }
}
