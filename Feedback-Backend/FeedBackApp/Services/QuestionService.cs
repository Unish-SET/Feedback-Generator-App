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
    public class QuestionService : IQuestionService
    {
        private readonly IRepository<Survey>         _surveyRepo;
        private readonly IRepository<Question>       _questionRepo;
        private readonly IRepository<QuestionOption> _optionRepo;
        private readonly IAuditService               _audit;
        private readonly IQuestionBankService        _bankService;

        public QuestionService(
            IRepository<Survey>         surveyRepo,
            IRepository<Question>       questionRepo,
            IRepository<QuestionOption> optionRepo,
            IAuditService               audit,
            IQuestionBankService        bankService)
        {
            _surveyRepo   = surveyRepo;
            _questionRepo = questionRepo;
            _optionRepo   = optionRepo;
            _audit        = audit;
            _bankService  = bankService;
        }

        public async Task<QuestionResponseDto> AddQuestionAsync(int surveyId, CreateQuestionDto dto, int userId, string role)
        {
            var survey = await ValidateSurveyAccess(surveyId, userId, role);

            if (survey.State != SurveyState.Inactive)
                throw new BadRequestException("Questions can only be added to Inactive surveys. Pause the survey first.");

            if (!Enum.TryParse<QuestionType>(dto.Type, true, out var questionType))
                throw new BadRequestException($"Invalid question type: {dto.Type}");

            var question = new Question
            {
                SurveyId   = surveyId,
                Text       = dto.Text,
                Type       = questionType,
                IsRequired = dto.IsRequired,
                Order      = dto.Order,
                Options    = (questionType == QuestionType.SingleChoice || questionType == QuestionType.MultipleChoice)
                    ? dto.Options.Select(o => new QuestionOption { Text = o.Text, Order = o.Order }).ToList()
                    : new List<QuestionOption>()
            };

            await _questionRepo.AddAsync(question);
            await _questionRepo.SaveChangesAsync();

            var result = await MapToResponseDto(question.Id);

            _ = _bankService.AutoSaveQuestionsAsync(new[] { question }, userId);
            _ = _audit.LogAsync("Create", "Question", question.Id.ToString(), userId);

            return result;
        }

        public async Task<QuestionResponseDto> UpdateQuestionAsync(int surveyId, int questionId, UpdateQuestionDto dto, int userId, string role)
        {
            var survey = await ValidateSurveyAccess(surveyId, userId, role);

            if (survey.State != SurveyState.Inactive)
                throw new BadRequestException("Questions can only be updated in Inactive surveys. Pause the survey first.");

            if (!Enum.TryParse<QuestionType>(dto.Type, true, out var questionType))
                throw new BadRequestException($"Invalid question type: {dto.Type}");

            var question = await _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyId == surveyId);

            if (question == null)
                throw new NotFoundException($"Question with ID {questionId} not found in survey {surveyId}.");

            question.Text       = dto.Text;
            question.Type       = questionType;
            question.IsRequired = dto.IsRequired;
            question.Order      = dto.Order;

            _optionRepo.RemoveRange(question.Options);

            if (questionType == QuestionType.SingleChoice || questionType == QuestionType.MultipleChoice)
            {
                foreach (var optionDto in dto.Options)
                {
                    var option = new QuestionOption
                    {
                        QuestionId = question.Id,
                        Text       = optionDto.Text,
                        Order      = optionDto.Order
                    };
                    await _optionRepo.AddAsync(option);
                }
            }

            _questionRepo.Update(question);
            await _questionRepo.SaveChangesAsync();

            var result = await MapToResponseDto(question.Id);

            _ = _bankService.AutoSaveQuestionsAsync(new[] { question }, userId);
            _ = _audit.LogAsync("Update", "Question", questionId.ToString(), userId);

            return result;
        }

        public async Task DeleteQuestionAsync(int surveyId, int questionId, int userId, string role)
        {
            var survey = await ValidateSurveyAccess(surveyId, userId, role);

            if (survey.State != SurveyState.Inactive)
                throw new BadRequestException("Questions can only be deleted from Inactive surveys. Pause the survey first.");

            var question = await _questionRepo.GetQueryable()
                .FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyId == surveyId);

            if (question == null)
                throw new NotFoundException($"Question with ID {questionId} not found in survey {surveyId}.");

            _questionRepo.Remove(question);
            await _questionRepo.SaveChangesAsync();
            _ = _audit.LogAsync("Delete", "Question", questionId.ToString(), userId);
        }

        public async Task<List<QuestionResponseDto>> GetQuestionsAsync(int surveyId, int userId, string role, string? typeFilter = null)
        {
            await ValidateSurveyAccess(surveyId, userId, role);

            var query = _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .Where(q => q.SurveyId == surveyId);

            if (!string.IsNullOrEmpty(typeFilter) && Enum.TryParse<QuestionType>(typeFilter, true, out var filterType))
                query = query.Where(q => q.Type == filterType);

            return await query
                .OrderBy(q => q.Order)
                .Select(q => new QuestionResponseDto
                {
                    Id         = q.Id,
                    Text       = q.Text,
                    Type       = q.Type.ToString(),
                    IsRequired = q.IsRequired,
                    Order      = q.Order,
                    Options    = q.Options.OrderBy(o => o.Order).Select(o => new OptionResponseDto
                    {
                        Id    = o.Id,
                        Text  = o.Text,
                        Order = o.Order
                    }).ToList()
                })
                .ToListAsync();
        }

        // ── Helpers ──

        private async Task<Survey> ValidateSurveyAccess(int surveyId, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey.");

            return survey;
        }

        private async Task<QuestionResponseDto> MapToResponseDto(int questionId)
        {
            var question = await _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .FirstAsync(q => q.Id == questionId);

            return new QuestionResponseDto
            {
                Id         = question.Id,
                Text       = question.Text,
                Type       = question.Type.ToString(),
                IsRequired = question.IsRequired,
                Order      = question.Order,
                Options    = question.Options.OrderBy(o => o.Order).Select(o => new OptionResponseDto
                {
                    Id    = o.Id,
                    Text  = o.Text,
                    Order = o.Order
                }).ToList()
            };
        }
    }
}
