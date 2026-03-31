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
    public class ResponseService : IResponseService
    {
        private readonly IRepository<Survey>         _surveyRepo;
        private readonly IRepository<Question>       _questionRepo;
        private readonly IRepository<SurveyResponse> _responseRepo;

        public ResponseService(
            IRepository<Survey>         surveyRepo,
            IRepository<Question>       questionRepo,
            IRepository<SurveyResponse> responseRepo)
        {
            _surveyRepo   = surveyRepo;
            _questionRepo = questionRepo;
            _responseRepo = responseRepo;
        }

        public async Task<ResponseListDto> SubmitAsync(Guid publicToken, SubmitResponseDto dto, int? userId)
        {
            // 1. Validate survey exists
            var survey = await _surveyRepo.GetQueryable()
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.PublicToken == publicToken);

            if (survey == null)
                throw new NotFoundException("Survey not found.");

            // 2. State must be Active
            if (survey.State != SurveyState.Active)
                throw new BadRequestException("This survey is not currently accepting responses.");

            // 3. Date range
            var now = DateTime.UtcNow;
            if (survey.StartDate.HasValue && DateTime.SpecifyKind(survey.StartDate.Value, DateTimeKind.Utc) > now)
                throw new BadRequestException("This survey has not started yet.");

            if (survey.EndDate.HasValue && DateTime.SpecifyKind(survey.EndDate.Value, DateTimeKind.Utc) < now)
                throw new BadRequestException("This survey has ended.");

            // 4. Anonymous check
            if (!userId.HasValue && !survey.AllowAnonymous)
                throw new ForbiddenException("This survey requires authentication to submit a response.");

            // 5. Duplicate check (authenticated users only)
            if (userId.HasValue)
            {
                var duplicate = await _responseRepo.AnyAsync(
                    r => r.SurveyId == survey.Id && r.UserId == userId.Value);

                if (duplicate)
                    throw new ConflictException("You have already submitted a response for this survey.");
            }

            // 6. Validate required questions
            var requiredQuestionIds = survey.Questions
                .Where(q => q.IsRequired)
                .Select(q => q.Id)
                .ToHashSet();

            var answeredQuestionIds = dto.Answers
                .Select(a => a.QuestionId)
                .ToHashSet();

            var missingRequired = requiredQuestionIds.Except(answeredQuestionIds).ToList();
            if (missingRequired.Any())
                throw new BadRequestException($"Required questions not answered: {string.Join(", ", missingRequired)}");

            // 7. Create response atomically
            var response = new SurveyResponse
            {
                SurveyId    = survey.Id,
                UserId      = userId,
                SubmittedAt = DateTime.UtcNow,
                Answers     = dto.Answers
                    .Where(a => survey.Questions.Any(q => q.Id == a.QuestionId))
                    .Select(a => new Answer
                    {
                        QuestionId        = a.QuestionId,
                        SelectedOptionId  = a.SelectedOptionId,
                        TextValue         = a.TextValue,
                        RatingValue       = a.RatingValue,
                        SelectedOptionIds = a.SelectedOptionIds != null
                            ? string.Join(",", a.SelectedOptionIds)
                            : null
                    }).ToList()
            };

            await _responseRepo.AddAsync(response);
            try
            {
                await _responseRepo.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
            {
                throw new ConflictException("You have already submitted a response for this survey.");
            }
            catch (DbUpdateException)
            {
                var freshSurvey = await _surveyRepo.FirstOrDefaultAsync(s => s.PublicToken == publicToken);
                if (freshSurvey != null && freshSurvey.State != SurveyState.Active)
                    throw new BadRequestException("This survey is no longer accepting responses.");
                throw new BadRequestException("Failed to save response. Please try again.");
            }

            return await MapToResponseListDto(response.Id);
        }

        public async Task<PaginatedResult<ResponseListDto>> GetResponsesAsync(int surveyId, ResponseFilterParams filter, int userId, string role)
        {
            if (filter.SubmittedFrom.HasValue && filter.SubmittedTo.HasValue &&
                filter.SubmittedFrom.Value > filter.SubmittedTo.Value)
                throw new BadRequestException("FromDate cannot be later than ToDate.");

            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey's responses.");

            var query = _responseRepo.GetQueryable()
                .Include(r => r.User)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.Question)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.SelectedOption)
                .Where(r => r.SurveyId == surveyId);

            if (filter.SubmittedFrom.HasValue)
            {
                var from = DateTime.SpecifyKind(filter.SubmittedFrom.Value.Date, DateTimeKind.Utc);
                query = query.Where(r => r.SubmittedAt >= from);
            }

            if (filter.SubmittedTo.HasValue)
            {
                var to = DateTime.SpecifyKind(
                    filter.SubmittedTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(r => r.SubmittedAt <= to);
            }

            if (filter.UserId.HasValue)
            {
                if (filter.UserId.Value == 0)
                    query = query.Where(r => r.UserId == null);
                else
                    query = query.Where(r => r.UserId == filter.UserId.Value);
            }

            var totalCount = await query.CountAsync();

            var rawItems = await query
                .OrderByDescending(r => r.SubmittedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => new
                {
                    r.Id,
                    r.SurveyId,
                    r.UserId,
                    Username    = r.User != null ? r.User.Username : null,
                    r.SubmittedAt,
                    Answers = r.Answers.Select(a => new
                    {
                        a.QuestionId,
                        QuestionText         = a.Question != null ? a.Question.Text : string.Empty,
                        QuestionType         = a.Question != null ? a.Question.Type.ToString() : string.Empty,
                        a.SelectedOptionId,
                        SelectedOptionText   = a.SelectedOption != null ? a.SelectedOption.Text : null,
                        a.TextValue,
                        a.RatingValue,
                        RawSelectedOptionIds = a.SelectedOptionIds
                    }).ToList()
                })
                .ToListAsync();

            var items = rawItems.Select(r => new ResponseListDto
            {
                Id          = r.Id,
                SurveyId    = r.SurveyId,
                UserId      = r.UserId,
                Username    = r.Username,
                SubmittedAt = r.SubmittedAt,
                Answers     = r.Answers.Select(a => new AnswerDto
                {
                    QuestionId         = a.QuestionId,
                    QuestionText       = a.QuestionText,
                    QuestionType       = a.QuestionType,
                    SelectedOptionId   = a.SelectedOptionId,
                    SelectedOptionText = a.SelectedOptionText,
                    TextValue          = a.TextValue,
                    RatingValue        = a.RatingValue,
                    SelectedOptionIds  = ParseOptionIds(a.RawSelectedOptionIds)
                }).ToList()
            }).ToList();

            return new PaginatedResult<ResponseListDto>
            {
                Items      = items,
                PageNumber = filter.PageNumber,
                PageSize   = filter.PageSize,
                TotalCount = totalCount
            };
        }

        private async Task<ResponseListDto> MapToResponseListDto(int responseId)
        {
            var response = await _responseRepo.GetQueryable()
                .Include(r => r.User)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.Question)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.SelectedOption)
                .FirstOrDefaultAsync(r => r.Id == responseId);

            if (response == null)
                throw new NotFoundException($"Response with ID {responseId} not found.");

            return new ResponseListDto
            {
                Id          = response.Id,
                SurveyId    = response.SurveyId,
                UserId      = response.UserId,
                Username    = response.User?.Username,
                SubmittedAt = response.SubmittedAt,
                Answers     = response.Answers.Select(a => new AnswerDto
                {
                    QuestionId         = a.QuestionId,
                    QuestionText       = a.Question?.Text ?? string.Empty,
                    QuestionType       = a.Question?.Type.ToString() ?? string.Empty,
                    SelectedOptionId   = a.SelectedOptionId,
                    SelectedOptionText = a.SelectedOption?.Text,
                    TextValue          = a.TextValue,
                    RatingValue        = a.RatingValue,
                    SelectedOptionIds  = ParseOptionIds(a.SelectedOptionIds)
                }).ToList()
            };
        }

        private static List<int>? ParseOptionIds(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var result = new List<int>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var id))
                    result.Add(id);
            return result.Count > 0 ? result : null;
        }
    }
}
