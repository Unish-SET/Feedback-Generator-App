using AutoMapper;
using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.DTOs;
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
            var surveyResponse = new SurveyResponseEntity
            {
                SurveyId = dto.SurveyId,
                RespondentUserId = userId,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                IsComplete = true
            };

            await _responseRepo.AddAsync(surveyResponse);

            foreach (var answerDto in dto.Answers)
            {
                var answer = new Answer
                {
                    SurveyResponseId = surveyResponse.Id,
                    QuestionId = answerDto.QuestionId,
                    AnswerText = answerDto.AnswerText
                };
                await _answerRepo.AddAsync(answer);
            }

            // Notify survey creator
            var survey = await _context.Surveys.FindAsync(dto.SurveyId);
            if (survey != null)
            {
                await _notificationService.CreateNotificationAsync(
                    survey.CreatedByUserId,
                    $"New response received for survey: {survey.Title}");
            }

            return await GetResponseByIdAsync(surveyResponse.Id) ?? throw new Exception("Failed to submit response.");
        }

        public async Task<SurveyResponseDetailDto?> GetResponseByIdAsync(int id)
        {
            var response = await _context.SurveyResponses
                .Include(r => r.Survey)
                .Include(r => r.Answers)
                    .ThenInclude(a => a.Question)
                .FirstOrDefaultAsync(r => r.Id == id);

            return response == null ? null : _mapper.Map<SurveyResponseDetailDto>(response);
        }

        public async Task<PagedResult<SurveyResponseDetailDto>> GetResponsesBySurveyAsync(int surveyId, PaginationParams paginationParams)
        {
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
            var response = await _responseRepo.GetByIdAsync(responseId);
            if (response == null) throw new KeyNotFoundException("Response not found.");

            response.IsComplete = false;
            response.CompletedAt = null;
            await _responseRepo.UpdateAsync(response);

            return await GetResponseByIdAsync(responseId) ?? throw new Exception("Failed to pause response.");
        }

        public async Task<SurveyResponseDetailDto> ResumeResponseAsync(int responseId, List<SubmitAnswerDto> additionalAnswers)
        {
            var response = await _responseRepo.GetByIdAsync(responseId);
            if (response == null) throw new KeyNotFoundException("Response not found.");

            foreach (var answerDto in additionalAnswers)
            {
                var answer = new Answer
                {
                    SurveyResponseId = responseId,
                    QuestionId = answerDto.QuestionId,
                    AnswerText = answerDto.AnswerText
                };
                await _answerRepo.AddAsync(answer);
            }

            response.IsComplete = true;
            response.CompletedAt = DateTime.UtcNow;
            await _responseRepo.UpdateAsync(response);

            return await GetResponseByIdAsync(responseId) ?? throw new Exception("Failed to resume response.");
        }
    }
}
