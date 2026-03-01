using AutoMapper;
using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Services
{
    public class SurveyService : ISurveyService
    {
        private readonly FeedbackDbContext _context;
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IRepository<Question> _questionRepo;
        private readonly IRepository<SurveyDistribution> _distributionRepo;
        private readonly IMapper _mapper;

        public SurveyService(FeedbackDbContext context, IRepository<Survey> surveyRepo, IRepository<Question> questionRepo, IRepository<SurveyDistribution> distributionRepo, IMapper mapper)
        {
            _context = context;
            _surveyRepo = surveyRepo;
            _questionRepo = questionRepo;
            _distributionRepo = distributionRepo;
            _mapper = mapper;
        }

        public async Task<SurveyResponseDto> CreateSurveyAsync(CreateSurveyDto dto, int userId)
        {
            // Validate title
            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new BadRequestException("Survey title is required.");

            if (dto.Title.Length < 3)
                throw new BadRequestException("Survey title must be at least 3 characters long.");

            // Validate questions if provided
            if (dto.Questions != null && dto.Questions.Any())
            {
                var validTypes = new[] { "MultipleChoice", "OpenText", "Rating", "YesNo" };
                foreach (var q in dto.Questions)
                {
                    if (string.IsNullOrWhiteSpace(q.Text))
                        throw new BadRequestException("Question text cannot be empty.");

                    if (!validTypes.Contains(q.QuestionType))
                        throw new BadRequestException($"Invalid question type '{q.QuestionType}'. Valid types are: {string.Join(", ", validTypes)}.");

                    if (q.QuestionType == "MultipleChoice" && string.IsNullOrWhiteSpace(q.Options))
                        throw new BadRequestException($"Multiple choice question '{q.Text}' must have options provided.");
                }
            }

            // Validate expiry date
            if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value <= DateTime.UtcNow)
                throw new BadRequestException("Expiry date must be in the future.");

            var survey = _mapper.Map<Survey>(dto);
            survey.CreatedByUserId = userId;
            survey.CreatedAt = DateTime.UtcNow;
            survey.UpdatedAt = DateTime.UtcNow;

            await _surveyRepo.AddAsync(survey);

            var questionsToAdd = new List<Question>();

            // Copy questions from an existing survey if requested
            if (dto.CopyQuestionsFromSurveyId.HasValue)
            {
                var sourceSurvey = await _context.Surveys
                    .Include(s => s.Questions)
                    .FirstOrDefaultAsync(s => s.Id == dto.CopyQuestionsFromSurveyId.Value);

                if (sourceSurvey == null)
                    throw new NotFoundException($"Source survey with ID {dto.CopyQuestionsFromSurveyId.Value} was not found. Cannot copy questions.");

                if (!sourceSurvey.Questions.Any())
                    throw new BadRequestException($"Source survey '{sourceSurvey.Title}' has no questions to copy.");

                foreach (var sourceQ in sourceSurvey.Questions.OrderBy(q => q.OrderIndex))
                {
                    questionsToAdd.Add(new Question
                    {
                        SurveyId = survey.Id,
                        Text = sourceQ.Text,
                        QuestionType = sourceQ.QuestionType,
                        Options = sourceQ.Options,
                        IsRequired = sourceQ.IsRequired,
                        OrderIndex = sourceQ.OrderIndex
                    });
                }
            }

            // Add any additional new questions from the DTO
            if (dto.Questions != null && dto.Questions.Any())
            {
                foreach (var qDto in dto.Questions)
                {
                    var question = _mapper.Map<Question>(qDto);
                    question.SurveyId = survey.Id;
                    questionsToAdd.Add(question);
                }
            }

            // Batch insert all questions in a single round-trip
            if (questionsToAdd.Any())
            {
                await _questionRepo.AddRangeAsync(questionsToAdd);
            }

            // Auto-generate shareable link
            var distribution = new SurveyDistribution
            {
                SurveyId = survey.Id,
                DistributionType = "Link",
                DistributionValue = $"/survey/respond/{survey.Id}?token={Guid.NewGuid()}",
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _distributionRepo.AddAsync(distribution);

            return await GetSurveyByIdAsync(survey.Id) ?? throw new BadRequestException("Failed to create survey.");
        }

        public async Task<SurveyResponseDto?> GetSurveyByIdAsync(int id)
        {
            if (id <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");

            var survey = await _context.Surveys
                .Include(s => s.CreatedByUser)
                .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
                .Include(s => s.SurveyDistributions)
                .Include(s => s.SurveyResponses)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (survey == null) return null;

            var dto = _mapper.Map<SurveyResponseDto>(survey);
            dto.TotalResponses = survey.SurveyResponses.Count;
            var link = survey.SurveyDistributions.FirstOrDefault(d => d.DistributionType == "Link");
            if (link != null) dto.ShareableLink = link.DistributionValue;

            return dto;
        }

        public async Task<PagedResult<SurveyResponseDto>> GetAllSurveysAsync(PaginationParams paginationParams)
        {
            if (paginationParams.PageNumber <= 0)
                throw new BadRequestException("Page number must be greater than 0.");
            if (paginationParams.PageSize <= 0 || paginationParams.PageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");

            var query = _context.Surveys
                .Include(s => s.CreatedByUser)
                .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
                .AsQueryable();

            // Filter by search term
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(s => s.Title.ToLower().Contains(search)
                    || s.Description.ToLower().Contains(search));
            }

            // Filter by question type
            if (!string.IsNullOrWhiteSpace(paginationParams.QuestionType))
            {
                var qType = paginationParams.QuestionType;
                query = query.Where(s => s.Questions.Any(q => q.QuestionType == qType));
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "title" => paginationParams.SortDescending ? query.OrderByDescending(s => s.Title) : query.OrderBy(s => s.Title),
                "createdat" => paginationParams.SortDescending ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
                _ => query.OrderByDescending(s => s.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var surveys = await query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToListAsync();

            return new PagedResult<SurveyResponseDto>
            {
                Items = _mapper.Map<List<SurveyResponseDto>>(surveys),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<PagedResult<SurveyResponseDto>> GetSurveysByUserAsync(int userId, PaginationParams paginationParams)
        {
            if (paginationParams.PageNumber <= 0)
                throw new BadRequestException("Page number must be greater than 0.");
            if (paginationParams.PageSize <= 0 || paginationParams.PageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");

            var query = _context.Surveys
                .Include(s => s.CreatedByUser)
                .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
                .Where(s => s.CreatedByUserId == userId)
                .AsQueryable();

            // Filter
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(s => s.Title.ToLower().Contains(search)
                    || s.Description.ToLower().Contains(search));
            }

            // Filter by question type
            if (!string.IsNullOrWhiteSpace(paginationParams.QuestionType))
            {
                var qType = paginationParams.QuestionType;
                query = query.Where(s => s.Questions.Any(q => q.QuestionType == qType));
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "title" => paginationParams.SortDescending ? query.OrderByDescending(s => s.Title) : query.OrderBy(s => s.Title),
                _ => query.OrderByDescending(s => s.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var surveys = await query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToListAsync();

            return new PagedResult<SurveyResponseDto>
            {
                Items = _mapper.Map<List<SurveyResponseDto>>(surveys),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<SurveyResponseDto?> UpdateSurveyAsync(int id, UpdateSurveyDto dto)
        {
            if (id <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");

            var survey = await _surveyRepo.GetByIdAsync(id);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {id} was not found.");

            if (dto.Title != null && dto.Title.Length < 3)
                throw new BadRequestException("Survey title must be at least 3 characters long.");

            if (dto.Title != null) survey.Title = dto.Title;
            if (dto.Description != null) survey.Description = dto.Description;
            if (dto.IsActive.HasValue) survey.IsActive = dto.IsActive.Value;
            if (dto.BrandingConfig != null) survey.BrandingConfig = dto.BrandingConfig;
            if (dto.ExpiresAt.HasValue)
            {
                if (dto.ExpiresAt.Value <= DateTime.UtcNow)
                    throw new BadRequestException("Expiry date must be in the future.");
                survey.ExpiresAt = dto.ExpiresAt.Value;
            }

            survey.Version++;
            survey.UpdatedAt = DateTime.UtcNow;

            await _surveyRepo.UpdateAsync(survey);
            return await GetSurveyByIdAsync(id);
        }

        public async Task<bool> DeleteSurveyAsync(int id)
        {
            if (id <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");

            var survey = await _surveyRepo.GetByIdAsync(id);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {id} was not found.");

            await _surveyRepo.DeleteAsync(survey);
            return true;
        }

        public async Task<QuestionResponseDto> AddQuestionAsync(int surveyId, CreateQuestionDto dto)
        {
            if (surveyId <= 0)
                throw new BadRequestException("Survey ID must be a positive number.");

            // Verify survey exists
            var survey = await _surveyRepo.GetByIdAsync(surveyId);
            if (survey == null)
                throw new NotFoundException($"Survey with ID {surveyId} was not found.");

            if (string.IsNullOrWhiteSpace(dto.Text))
                throw new BadRequestException("Question text is required.");

            var validTypes = new[] { "MultipleChoice", "OpenText", "Rating", "YesNo" };
            if (!validTypes.Contains(dto.QuestionType))
                throw new BadRequestException($"Invalid question type '{dto.QuestionType}'. Valid types are: {string.Join(", ", validTypes)}.");

            if (dto.QuestionType == "MultipleChoice" && string.IsNullOrWhiteSpace(dto.Options))
                throw new BadRequestException("Multiple choice questions must have options provided.");

            var question = _mapper.Map<Question>(dto);
            question.SurveyId = surveyId;

            await _questionRepo.AddAsync(question);
            return _mapper.Map<QuestionResponseDto>(question);
        }

        public async Task<bool> DeleteQuestionAsync(int questionId)
        {
            if (questionId <= 0)
                throw new BadRequestException("Question ID must be a positive number.");

            var question = await _questionRepo.GetByIdAsync(questionId);
            if (question == null)
                throw new NotFoundException($"Question with ID {questionId} was not found.");

            await _questionRepo.DeleteAsync(question);
            return true;
        }
    }
}
