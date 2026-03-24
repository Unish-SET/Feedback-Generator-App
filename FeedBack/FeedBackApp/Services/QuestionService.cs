using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IRepository<SurveyVersion> _versionRepo;
        private readonly IRepository<Question> _questionRepo;
        private readonly IRepository<QuestionOption> _optionRepo;

        public QuestionService(
            IRepository<Survey> surveyRepo,
            IRepository<SurveyVersion> versionRepo,
            IRepository<Question> questionRepo,
            IRepository<QuestionOption> optionRepo)
        {
            _surveyRepo = surveyRepo;
            _versionRepo = versionRepo;
            _questionRepo = questionRepo;
            _optionRepo = optionRepo;
        }

        public async Task<QuestionResponseDto> AddQuestionAsync(int surveyId, CreateQuestionDto dto, int userId, string role)
        {
            var survey = await ValidateSurveyAccess(surveyId, userId, role);

            if (survey.Status != SurveyStatus.Draft)
                throw new BadRequestException("Questions can only be added to Draft surveys.");

            if (!Enum.TryParse<QuestionType>(dto.Type, true, out var questionType))
                throw new BadRequestException($"Invalid question type: {dto.Type}");

            var latestVersion = await GetLatestVersion(surveyId);

            var question = new Question
            {
                SurveyVersionId = latestVersion.Id,
                Text = dto.Text,
                Type = questionType,
                IsRequired = dto.IsRequired,
                Order = dto.Order
            };

            await _questionRepo.AddAsync(question);
            await _questionRepo.SaveChangesAsync();

            if (questionType == QuestionType.SingleChoice || questionType == QuestionType.MultipleChoice)
            {
                foreach (var optionDto in dto.Options)
                {
                    var option = new QuestionOption
                    {
                        QuestionId = question.Id,
                        Text = optionDto.Text,
                        Order = optionDto.Order
                    };
                    await _optionRepo.AddAsync(option);
                }
                await _optionRepo.SaveChangesAsync();
            }

            return await MapToResponseDto(question.Id);
        }

        public async Task<QuestionResponseDto> UpdateQuestionAsync(int surveyId, int questionId, UpdateQuestionDto dto, int userId, string role)
        {
            var survey = await ValidateSurveyAccess(surveyId, userId, role);

            if (survey.Status != SurveyStatus.Draft)
                throw new BadRequestException("Questions can only be updated in Draft surveys.");

            if (!Enum.TryParse<QuestionType>(dto.Type, true, out var questionType))
                throw new BadRequestException($"Invalid question type: {dto.Type}");

            var question = await _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .Include(q => q.SurveyVersion)
                .FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyVersion.SurveyId == surveyId);

            if (question == null)
                throw new NotFoundException($"Question with ID {questionId} not found in survey {surveyId}.");

            question.Text = dto.Text;
            question.Type = questionType;
            question.IsRequired = dto.IsRequired;
            question.Order = dto.Order;

            _optionRepo.RemoveRange(question.Options);

            if (questionType == QuestionType.SingleChoice || questionType == QuestionType.MultipleChoice)
            {
                foreach (var optionDto in dto.Options)
                {
                    var option = new QuestionOption
                    {
                        QuestionId = question.Id,
                        Text = optionDto.Text,
                        Order = optionDto.Order
                    };
                    await _optionRepo.AddAsync(option);
                }
            }

            _questionRepo.Update(question);
            await _questionRepo.SaveChangesAsync();
            return await MapToResponseDto(question.Id);
        }

        public async Task DeleteQuestionAsync(int surveyId, int questionId, int userId, string role)
        {
            var survey = await ValidateSurveyAccess(surveyId, userId, role);

            if (survey.Status != SurveyStatus.Draft)
                throw new BadRequestException("Questions can only be deleted from Draft surveys.");

            var question = await _questionRepo.GetQueryable()
                .Include(q => q.SurveyVersion)
                .FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyVersion.SurveyId == surveyId);

            if (question == null)
                throw new NotFoundException($"Question with ID {questionId} not found in survey {surveyId}.");

            _questionRepo.Remove(question);
            await _questionRepo.SaveChangesAsync();
        }

        public async Task<List<QuestionResponseDto>> GetQuestionsAsync(int surveyId, int userId, string role, string? typeFilter = null)
        {
            await ValidateSurveyAccess(surveyId, userId, role);
            var latestVersion = await GetLatestVersion(surveyId);

            var query = _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .Where(q => q.SurveyVersionId == latestVersion.Id);

            if (!string.IsNullOrEmpty(typeFilter) && Enum.TryParse<QuestionType>(typeFilter, true, out var filterType))
            {
                query = query.Where(q => q.Type == filterType);
            }

            var questions = await query
                .OrderBy(q => q.Order)
                .Select(q => new QuestionResponseDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    Type = q.Type.ToString(),
                    IsRequired = q.IsRequired,
                    Order = q.Order,
                    Options = q.Options.OrderBy(o => o.Order).Select(o => new OptionResponseDto
                    {
                        Id = o.Id,
                        Text = o.Text,
                        Order = o.Order
                    }).ToList()
                })
                .ToListAsync();

            return questions;
        }

        // ── Helpers ──

        private async Task<Survey> ValidateSurveyAccess(int surveyId, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} not found.");

            if (role != UserRole.Admin.ToString() && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey.");

            return survey;
        }

        private async Task<SurveyVersion> GetLatestVersion(int surveyId)
        {
            var version = await _versionRepo.GetQueryable()
                .Where(v => v.SurveyId == surveyId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            if (version == null)
                throw new BadRequestException("Survey has no version.");

            return version;
        }

        private async Task<QuestionResponseDto> MapToResponseDto(int questionId)
        {
            var question = await _questionRepo.GetQueryable()
                .Include(q => q.Options)
                .FirstAsync(q => q.Id == questionId);

            return new QuestionResponseDto
            {
                Id = question.Id,
                Text = question.Text,
                Type = question.Type.ToString(),
                IsRequired = question.IsRequired,
                Order = question.Order,
                Options = question.Options.OrderBy(o => o.Order).Select(o => new OptionResponseDto
                {
                    Id = o.Id,
                    Text = o.Text,
                    Order = o.Order
                }).ToList()
            };
        }
    }
}
