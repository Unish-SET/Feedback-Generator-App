using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class ResponseService : IResponseService
    {
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IRepository<SurveyVersion> _versionRepo;
        private readonly IRepository<SurveyResponse> _responseRepo;
        private readonly IRepository<Answer> _answerRepo;

        public ResponseService(
            IRepository<Survey> surveyRepo,
            IRepository<SurveyVersion> versionRepo,
            IRepository<SurveyResponse> responseRepo,
            IRepository<Answer> answerRepo)
        {
            _surveyRepo = surveyRepo;
            _versionRepo = versionRepo;
            _responseRepo = responseRepo;
            _answerRepo = answerRepo;
        }

        public async Task<ResponseListDto> SubmitAsync(Guid publicToken, SubmitResponseDto dto, int? userId)
        {
            // 1. Validate survey exists
            var survey = await _surveyRepo.FirstOrDefaultAsync(s => s.PublicToken == publicToken);
            if (survey == null)
                throw new NotFoundException("Survey not found.");

            // 2. Validate survey is Active
            if (survey.Status != SurveyStatus.Active)
                throw new BadRequestException("This survey is not currently accepting responses.");

            // 3. Validate date range (dates are stored as UTC; SpecifyKind guards against EF returning Unspecified)
            var now = DateTime.UtcNow;
            if (survey.StartDate.HasValue && DateTime.SpecifyKind(survey.StartDate.Value, DateTimeKind.Utc) > now)
                throw new BadRequestException("This survey has not started yet.");

            if (survey.EndDate.HasValue && DateTime.SpecifyKind(survey.EndDate.Value, DateTimeKind.Utc) < now)
                throw new BadRequestException("This survey has ended.");

            // 4. Check anonymous allowed
            if (!userId.HasValue && !survey.AllowAnonymous)
                throw new ForbiddenException("This survey requires authentication to submit a response.");

            // 5. Validate version
            var surveyVersion = await _versionRepo.GetQueryable()
                .Include(v => v.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(v => v.Id == dto.SurveyVersionId && v.SurveyId == survey.Id);

            if (surveyVersion == null)
                throw new NotFoundException("Invalid survey version.");

            // 6. Duplicate check
            if (userId.HasValue)
            {
                var duplicate = await _responseRepo.AnyAsync(
                    r => r.SurveyVersionId == dto.SurveyVersionId && r.UserId == userId.Value);

                if (duplicate)
                    throw new ConflictException("You have already submitted a response for this survey version.");
            }

            // 7. Validate required questions
            var requiredQuestionIds = surveyVersion.Questions
                .Where(q => q.IsRequired)
                .Select(q => q.Id)
                .ToHashSet();

            var answeredQuestionIds = dto.Answers
                .Select(a => a.QuestionId)
                .ToHashSet();

            var missingRequired = requiredQuestionIds.Except(answeredQuestionIds).ToList();
            if (missingRequired.Any())
                throw new BadRequestException($"Required questions not answered: {string.Join(", ", missingRequired)}");

            // 8. Create response
            var response = new SurveyResponse
            {
                SurveyVersionId = dto.SurveyVersionId,
                UserId = userId,
                SubmittedAt = DateTime.UtcNow
            };

            await _responseRepo.AddAsync(response);
            await _responseRepo.SaveChangesAsync();

            // 9. Save answers
            foreach (var answerDto in dto.Answers)
            {
                var question = surveyVersion.Questions.FirstOrDefault(q => q.Id == answerDto.QuestionId);
                if (question == null) continue;

                var answer = new Answer
                {
                    ResponseId = response.Id,
                    QuestionId = answerDto.QuestionId,
                    SelectedOptionId = answerDto.SelectedOptionId,
                    TextValue = answerDto.TextValue,
                    RatingValue = answerDto.RatingValue,
                    SelectedOptionIds = answerDto.SelectedOptionIds != null
                        ? string.Join(",", answerDto.SelectedOptionIds)
                        : null
                };

                await _answerRepo.AddAsync(answer);
            }

            await _answerRepo.SaveChangesAsync();
            return await MapToResponseListDto(response.Id);
        }

        public async Task<PaginatedResult<ResponseListDto>> GetResponsesAsync(int surveyId, PaginationParams pagination, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (role != UserRole.Admin.ToString() && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey's responses.");

            var query = _responseRepo.GetQueryable()
                .Include(r => r.SurveyVersion)
                .Include(r => r.User)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.Question)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.SelectedOption)
                .Where(r => r.SurveyVersion.SurveyId == surveyId);

            var totalCount = await query.CountAsync();

            var responses = await query
                .OrderByDescending(r => r.SubmittedAt)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            var items = responses.Select(r => new ResponseListDto
            {
                Id = r.Id,
                SurveyVersionId = r.SurveyVersionId,
                VersionNumber = r.SurveyVersion.VersionNumber,
                UserId = r.UserId,
                Username = r.User?.Username,
                SubmittedAt = r.SubmittedAt,
                Answers = r.Answers.Select(a => new AnswerDto
                {
                    QuestionId = a.QuestionId,
                    QuestionText = a.Question?.Text ?? string.Empty,
                    QuestionType = a.Question?.Type.ToString() ?? string.Empty,
                    SelectedOptionId = a.SelectedOptionId,
                    SelectedOptionText = a.SelectedOption?.Text,
                    TextValue = a.TextValue,
                    RatingValue = a.RatingValue,
                    SelectedOptionIds = ParseOptionIds(a.SelectedOptionIds)
                }).ToList()
            }).ToList();

            return new PaginatedResult<ResponseListDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount
            };
        }

        private async Task<ResponseListDto> MapToResponseListDto(int responseId)
        {
            var response = await _responseRepo.GetQueryable()
                .Include(r => r.SurveyVersion)
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
                Id = response.Id,
                SurveyVersionId = response.SurveyVersionId,
                VersionNumber = response.SurveyVersion?.VersionNumber ?? 0,
                UserId = response.UserId,
                Username = response.User?.Username,
                SubmittedAt = response.SubmittedAt,
                Answers = response.Answers.Select(a => new AnswerDto
                {
                    QuestionId = a.QuestionId,
                    QuestionText = a.Question?.Text ?? string.Empty,
                    QuestionType = a.Question?.Type.ToString() ?? string.Empty,
                    SelectedOptionId = a.SelectedOptionId,
                    SelectedOptionText = a.SelectedOption?.Text,
                    TextValue = a.TextValue,
                    RatingValue = a.RatingValue,
                    SelectedOptionIds = ParseOptionIds(a.SelectedOptionIds)
                }).ToList()
            };
        }

        /// <summary>Safely parses comma-separated option IDs, ignoring malformed entries.</summary>
        private static List<int>? ParseOptionIds(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var result = new List<int>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var id))
                    result.Add(id);
            }
            return result.Count > 0 ? result : null;
        }
    }
}
