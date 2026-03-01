using AutoMapper;
using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;
using SurveyResponseEntity = FeedBackGeneratorApp.Models.SurveyResponse;

namespace FeedBackGeneratorApp.Services
{
    public class SurveyResponseService : ISurveyResponseService
    {
        private readonly FeedbackDbContext _context;
        private readonly IRepository<SurveyResponseEntity> _responseRepo;
        private readonly IRepository<Answer> _answerRepo;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public SurveyResponseService(
            FeedbackDbContext context,
            IRepository<SurveyResponseEntity> responseRepo,
            IRepository<Answer> answerRepo,
            INotificationService notificationService,
            IMapper mapper)
        {
            _context = context;
            _responseRepo = responseRepo;
            _answerRepo = answerRepo;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public async Task<SurveyResponseDetailDto> SubmitResponseAsync(SubmitResponseDto dto, int? userId)
        {
            if (dto.SurveyId <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");

            // Verify survey exists and is active
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == dto.SurveyId);

            if (survey == null)
                throw new NotFoundException($"Survey with ID {dto.SurveyId} was not found.");

            if (!survey.IsActive)
                throw new BadRequestException("This survey is no longer accepting responses.");

            if (survey.ExpiresAt.HasValue && survey.ExpiresAt.Value <= DateTime.UtcNow)
                throw new BadRequestException($"This survey expired on {survey.ExpiresAt.Value:yyyy-MM-dd HH:mm} UTC. Responses are no longer accepted.");

            // Validate answers
            if (dto.Answers == null || !dto.Answers.Any())
                throw new BadRequestException("At least one answer is required.");

            // Check required questions are answered
            var requiredQuestionIds = survey.Questions
                .Where(q => q.IsRequired)
                .Select(q => q.Id)
                .ToList();

            var answeredQuestionIds = dto.Answers.Select(a => a.QuestionId).ToList();
            var missingRequired = requiredQuestionIds.Except(answeredQuestionIds).ToList();

            if (missingRequired.Any())
                throw new BadRequestException($"Required question(s) not answered: {string.Join(", ", missingRequired)}.");

            // Validate each answer references a valid question
            var validQuestionIds = survey.Questions.Select(q => q.Id).ToList();
            foreach (var answer in dto.Answers)
            {
                if (!validQuestionIds.Contains(answer.QuestionId))
                    throw new BadRequestException($"Question ID {answer.QuestionId} does not belong to survey {dto.SurveyId}.");

                if (string.IsNullOrWhiteSpace(answer.AnswerText))
                    throw new BadRequestException($"Answer for question ID {answer.QuestionId} cannot be empty.");
            }

            var surveyResponse = new SurveyResponseEntity
            {
                SurveyId = dto.SurveyId,
                RespondentUserId = userId,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                IsComplete = true
            };

            await _responseRepo.AddAsync(surveyResponse);

            var answers = dto.Answers.Select(answerDto => new Answer
            {
                SurveyResponseId = surveyResponse.Id,
                QuestionId = answerDto.QuestionId,
                AnswerText = answerDto.AnswerText
            }).ToList();

            await _answerRepo.AddRangeAsync(answers);

            // Notify survey creator
            await _notificationService.CreateNotificationAsync(
                survey.CreatedByUserId,
                $"New response received for survey: {survey.Title}");

            return await GetResponseByIdAsync(surveyResponse.Id) ?? throw new BadRequestException("Failed to submit response.");
        }

        public async Task<SurveyResponseDetailDto?> GetResponseByIdAsync(int id)
        {
            if (id <= 0)
                throw new BadRequestException("Response ID must be a positive number.");

            var response = await _context.SurveyResponses
                .Include(r => r.Survey)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.Question)
                .FirstOrDefaultAsync(r => r.Id == id);

            return response == null ? null : _mapper.Map<SurveyResponseDetailDto>(response);
        }

        public async Task<PagedResult<SurveyResponseDetailDto>> GetResponsesBySurveyAsync(int surveyId, PaginationParams paginationParams)
        {
            if (surveyId <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");
            if (paginationParams.PageNumber <= 0)
                throw new BadRequestException("Page number must be greater than 0.");
            if (paginationParams.PageSize <= 0 || paginationParams.PageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");

            // Verify survey exists
            var surveyExists = await _context.Surveys.AnyAsync(s => s.Id == surveyId);
            if (!surveyExists)
                throw new NotFoundException($"Survey with ID {surveyId} was not found.");

            var query = _context.SurveyResponses
                .Include(r => r.Survey)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.Question)
                .Where(r => r.SurveyId == surveyId)
                .AsQueryable();

            // Filter by completion status
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                if (search == "complete" || search == "completed")
                    query = query.Where(r => r.IsComplete == true);
                else if (search == "incomplete" || search == "paused")
                    query = query.Where(r => r.IsComplete == false);
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "startedat" => paginationParams.SortDescending ? query.OrderByDescending(r => r.StartedAt) : query.OrderBy(r => r.StartedAt),
                "completedat" => paginationParams.SortDescending ? query.OrderByDescending(r => r.CompletedAt) : query.OrderBy(r => r.CompletedAt),
                _ => query.OrderByDescending(r => r.StartedAt)
            };

            var totalCount = await query.CountAsync();

            var responses = await query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToListAsync();

            return new PagedResult<SurveyResponseDetailDto>
            {
                Items = _mapper.Map<List<SurveyResponseDetailDto>>(responses),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<SurveyResponseDetailDto> PauseResponseAsync(int responseId)
        {
            if (responseId <= 0)
                throw new BadRequestException("Response ID must be a positive number.");

            var response = await _responseRepo.GetByIdAsync(responseId);
            if (response == null) throw new NotFoundException($"Response with ID {responseId} was not found.");

            if (!response.IsComplete)
                throw new BadRequestException("This response is already paused.");

            response.IsComplete = false;
            response.CompletedAt = null;
            await _responseRepo.UpdateAsync(response);

            return await GetResponseByIdAsync(responseId) ?? throw new BadRequestException("Failed to pause response.");
        }

        public async Task<SurveyResponseDetailDto> ResumeResponseAsync(int responseId, List<SubmitAnswerDto> additionalAnswers)
        {
            if (responseId <= 0)
                throw new BadRequestException("Response ID must be a positive number.");

            var response = await _responseRepo.GetByIdAsync(responseId);
            if (response == null) throw new NotFoundException($"Response with ID {responseId} was not found.");

            if (response.IsComplete)
                throw new BadRequestException("This response is already completed. You cannot resume a completed response.");

            if (additionalAnswers == null || !additionalAnswers.Any())
                throw new BadRequestException("At least one answer is required to resume.");

            var answers = additionalAnswers.Select(answerDto => new Answer
            {
                SurveyResponseId = responseId,
                QuestionId = answerDto.QuestionId,
                AnswerText = answerDto.AnswerText
            }).ToList();

            await _answerRepo.AddRangeAsync(answers);

            response.IsComplete = true;
            response.CompletedAt = DateTime.UtcNow;
            await _responseRepo.UpdateAsync(response);

            return await GetResponseByIdAsync(responseId) ?? throw new BadRequestException("Failed to resume response.");
        }
    }
}
