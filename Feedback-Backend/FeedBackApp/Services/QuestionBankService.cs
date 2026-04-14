using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Services
{
    public class QuestionBankService : IQuestionBankService
    {
        private readonly IRepository<BankQuestion>       _bankRepo;
        private readonly IRepository<BankQuestionOption> _bankOptionRepo;
        private readonly IRepository<Survey>             _surveyRepo;
        private readonly IRepository<Question>           _questionRepo;
        private readonly IAuditService                   _audit;
        private readonly ILogger<QuestionBankService>    _logger;

        public QuestionBankService(
            IRepository<BankQuestion>       bankRepo,
            IRepository<BankQuestionOption> bankOptionRepo,
            IRepository<Survey>             surveyRepo,
            IRepository<Question>           questionRepo,
            IAuditService                   audit,
            ILogger<QuestionBankService>    logger)
        {
            _bankRepo       = bankRepo;
            _bankOptionRepo = bankOptionRepo;
            _surveyRepo     = surveyRepo;
            _questionRepo   = questionRepo;
            _audit          = audit;
            _logger         = logger;
        }


        public async Task<BankQuestionDto> CreateAsync(CreateBankQuestionDto dto, int userId)
        {
            if (!Enum.TryParse<QuestionType>(dto.Type, true, out var questionType))
                throw new BadRequestException($"Invalid question type: {dto.Type}");

            var bq = new BankQuestion
            {
                CreatedBy  = userId,
                Text       = dto.Text,
                Type       = questionType,
                IsRequired = dto.IsRequired,
                Tag        = dto.Tag?.Trim(),
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
                Options    = BuildOptions(questionType, dto.Options)
            };

            await _bankRepo.AddAsync(bq);
            await _bankRepo.SaveChangesAsync();

            _ = _audit.LogAsync("Create", "BankQuestion", bq.Id.ToString(), userId);
            return ToDto(bq);
        }

        public async Task<BankQuestionDto> UpdateAsync(int id, UpdateBankQuestionDto dto, int userId, string role)
        {
            var bq = await GetWithAccessCheck(id, userId, role);

            if (!Enum.TryParse<QuestionType>(dto.Type, true, out var questionType))
                throw new BadRequestException($"Invalid question type: {dto.Type}");

            bq.Text       = dto.Text;
            bq.Type       = questionType;
            bq.IsRequired = dto.IsRequired;
            bq.Tag        = dto.Tag?.Trim();
            bq.UpdatedAt  = DateTime.UtcNow;

            _bankOptionRepo.RemoveRange(bq.Options);
            bq.Options = BuildOptions(questionType, dto.Options);

            _bankRepo.Update(bq);
            await _bankRepo.SaveChangesAsync();

            _ = _audit.LogAsync("Update", "BankQuestion", id.ToString(), userId);
            return ToDto(bq);
        }

        public async Task DeleteAsync(int id, int userId, string role)
        {
            var bq = await GetWithAccessCheck(id, userId, role);
            bq.IsDeleted = true;
            bq.UpdatedAt = DateTime.UtcNow;
            _bankRepo.Update(bq);
            await _bankRepo.SaveChangesAsync();
            _ = _audit.LogAsync("Delete", "BankQuestion", id.ToString(), userId);
        }

        public async Task<BankQuestionDto> GetByIdAsync(int id, int userId, string role)
        {
            var bq = await _bankRepo.GetQueryable()
                .Include(b => b.Options)
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (bq == null)
                throw new NotFoundException($"Bank question {id} not found.");

           
            if (!RoleHelper.IsAdmin(role) && bq.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this bank question.");

            return ToDto(bq);
        }

        public async Task<PaginatedResult<BankQuestionDto>> GetAllAsync(
            BankQuestionFilterParams filter, int userId, string role)
        {
            var page     = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            var pageSize = Math.Min(filter.PageSize <= 0 ? 20 : filter.PageSize, 50);

            var query = _bankRepo.GetQueryable()
                .Include(b => b.Options)
                .Where(b => !b.IsDeleted)
                .AsQueryable();

            if (!RoleHelper.IsAdmin(role))
                query = query.Where(b => b.CreatedBy == userId);

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(b => b.Text.Contains(filter.Search));

            if (!string.IsNullOrWhiteSpace(filter.Tag))
                query = query.Where(b => b.Tag == filter.Tag);

            if (!string.IsNullOrWhiteSpace(filter.Type) &&
                Enum.TryParse<QuestionType>(filter.Type, true, out var typeEnum))
                query = query.Where(b => b.Type == typeEnum);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BankQuestionDto
                {
                    Id         = b.Id,
                    Text       = b.Text,
                    Type       = b.Type.ToString(),
                    IsRequired = b.IsRequired,
                    Tag        = b.Tag,
                    CreatedAt  = b.CreatedAt,
                    Options    = b.Options.OrderBy(o => o.Order).Select(o => new OptionResponseDto
                    {
                        Id    = o.Id,
                        Text  = o.Text,
                        Order = o.Order
                    }).ToList()
                })
                .ToListAsync();

            return new PaginatedResult<BankQuestionDto>
            {
                Items      = items,
                PageNumber = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        // ── Clone from bank into a survey ─────────────────────────────────────

        public async Task<CloneFromBankResultDto> CloneIntoSurveyAsync(
            CloneFromBankDto dto, int userId, string role)
        {
            // 1. Validate target survey — must exist, be accessible, and be Draft
            var survey = await _surveyRepo.GetByIdAsync(dto.TargetSurveyId);
            if (survey == null)
                throw new NotFoundException($"Survey {dto.TargetSurveyId} not found.");

            if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this survey.");

            if (survey.State != SurveyState.Inactive)
                throw new BadRequestException("Questions can only be cloned into an Inactive survey.");

            // 2. Compute starting order — append after existing questions
            var maxOrder = await _questionRepo.GetQueryable()
                .Where(q => q.SurveyId == dto.TargetSurveyId)
                .Select(q => (int?)q.Order)
                .MaxAsync();
            int nextOrder = (maxOrder ?? 0) + 1;

            // 3. Load all requested bank questions in ONE query — no N+1
           
            var distinctIds = dto.BankQuestionIds.Distinct().ToList();

            var bankQuestions = await _bankRepo.GetQueryable()
                .Include(b => b.Options)
                .Where(b => distinctIds.Contains(b.Id) && !b.IsDeleted)
                .ToListAsync();

            // 4. Validate all IDs were found
            var foundIds   = bankQuestions.Select(b => b.Id).ToHashSet();
            var missingIds = distinctIds.Where(id => !foundIds.Contains(id)).ToList();
            if (missingIds.Any())
                throw new NotFoundException(
                    $"Bank question(s) not found: {string.Join(", ", missingIds)}");

            // 5. Non-admins can only clone their own bank questions
            if (!RoleHelper.IsAdmin(role))
            {
                var unauthorized = bankQuestions.Where(b => b.CreatedBy != userId).ToList();
                if (unauthorized.Any())
                    throw new ForbiddenException(
                        $"You do not own bank question(s): {string.Join(", ", unauthorized.Select(b => b.Id))}");
            }

            // 6. Build new Question entities preserving caller's requested sequence
            var newQuestions = dto.BankQuestionIds
                .Select((bankId, index) =>
                {
                    var source = bankQuestions.First(b => b.Id == bankId);
                    return new Question
                    {
                        SurveyId   = dto.TargetSurveyId,
                        Text       = source.Text,
                        Type       = source.Type,
                        IsRequired = source.IsRequired,
                        Order      = nextOrder + index,
                        Options    = (source.Type == QuestionType.SingleChoice ||
                                     source.Type == QuestionType.MultipleChoice)
                            ? source.Options
                                .OrderBy(o => o.Order)
                                .Select(o => new QuestionOption { Text = o.Text, Order = o.Order })
                                .ToList()
                            : new List<QuestionOption>()
                    };
                }).ToList();

            // 7. Single SaveChangesAsync — atomic
            foreach (var q in newQuestions)
                await _questionRepo.AddAsync(q);

            await _questionRepo.SaveChangesAsync();

            _ = _audit.LogAsync(
                action:     "CloneFromBank",
                entityName: "Survey",
                entityId:   dto.TargetSurveyId.ToString(),
                userId:     userId,
                newValues:  $"{{\"clonedCount\":{newQuestions.Count},\"surveyId\":{dto.TargetSurveyId}}}");

            return new CloneFromBankResultDto
            {
                NewQuestionIds = newQuestions.Select(q => q.Id).ToList(),
                ClonedCount    = newQuestions.Count,
                Message        = $"{newQuestions.Count} question(s) cloned from bank successfully."
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task<BankQuestion> GetWithAccessCheck(int id, int userId, string role)
        {
            // BUG-10 FIX: missing !b.IsDeleted — soft-deleted questions were editable/deletable again
            var bq = await _bankRepo.GetQueryable()
                .Include(b => b.Options)
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (bq == null)
                throw new NotFoundException($"Bank question {id} not found.");

            if (!RoleHelper.IsAdmin(role) && bq.CreatedBy != userId)
                throw new ForbiddenException("You do not have access to this bank question.");

            return bq;
        }

        private static List<BankQuestionOption> BuildOptions(
            QuestionType type, List<CreateOptionDto> optionDtos)
        {
            if (type != QuestionType.SingleChoice && type != QuestionType.MultipleChoice)
                return new List<BankQuestionOption>();

            return optionDtos
                .Select(o => new BankQuestionOption { Text = o.Text, Order = o.Order })
                .ToList();
        }

        private static BankQuestionDto ToDto(BankQuestion bq) => new()
        {
            Id         = bq.Id,
            Text       = bq.Text,
            Type       = bq.Type.ToString(),
            IsRequired = bq.IsRequired,
            Tag        = bq.Tag,
            CreatedAt  = bq.CreatedAt,
            Options    = bq.Options.OrderBy(o => o.Order).Select(o => new OptionResponseDto
            {
                Id    = o.Id,
                Text  = o.Text,
                Order = o.Order
            }).ToList()
        };

        // ── Auto-Save (deduplication by hash) ─────────────────────────────────

        public async Task AutoSaveQuestionsAsync(IEnumerable<Question> questions, int userId)
        {
            try
            {
            var questionList = questions.ToList();
            if (questionList.Count == 0) return;

            // 1. Compute hashes for all incoming questions
            var incoming = questionList.Select(q => new
            {
                Question = q,
                Hash     = ComputeQuestionHash(q.Text, q.Type.ToString(),
                               q.Options?.OrderBy(o => o.Order).Select(o => o.Text).ToList()
                               ?? new List<string>())
            }).ToList();

            var hashes = incoming.Select(x => x.Hash).Distinct().ToList();

            // 2. Find which hashes already exist in the bank for this user
            var existingHashes = await _bankRepo.GetQueryable()
                .Where(b => b.CreatedBy == userId && b.Hash != null && hashes.Contains(b.Hash))
                .Select(b => b.Hash!)
                .ToListAsync();

            var existingSet = existingHashes.ToHashSet();

            // 3. Insert only genuinely new questions
            var seen = new HashSet<string>();
            foreach (var item in incoming)
            {
                if (existingSet.Contains(item.Hash)) continue;
                if (!seen.Add(item.Hash)) continue;

                var bq = new BankQuestion
                {
                    CreatedBy  = userId,
                    Text       = item.Question.Text,
                    Type       = item.Question.Type,
                    IsRequired = item.Question.IsRequired,
                    Hash       = item.Hash,
                    CreatedAt  = DateTime.UtcNow,
                    UpdatedAt  = DateTime.UtcNow,
                    Options    = (item.Question.Type == QuestionType.SingleChoice ||
                                  item.Question.Type == QuestionType.MultipleChoice)
                        ? item.Question.Options
                            .OrderBy(o => o.Order)
                            .Select(o => new BankQuestionOption { Text = o.Text, Order = o.Order })
                            .ToList()
                        : new List<BankQuestionOption>()
                };

                await _bankRepo.AddAsync(bq);
                _logger.LogInformation("[QuestionBank] Auto-saved question '{Text}' for user {UserId}", item.Question.Text, userId);
            }

            await _bankRepo.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QuestionBank] AutoSave failed for user {UserId}", userId);
            }
        }

        
        private static string ComputeQuestionHash(string text, string type, List<string> optionTexts)
        {
            var normalized = $"{text.Trim().ToLowerInvariant()}|{type.ToLowerInvariant()}|{string.Join("|", optionTexts.Select(o => o.Trim().ToLowerInvariant()))}";
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
