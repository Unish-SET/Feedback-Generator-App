using AutoMapper;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Services
{
    public class RecipientService : IRecipientService
    {
        private readonly IRepository<Recipient> _recipientRepo;
        private readonly IMapper _mapper;

        public RecipientService(IRepository<Recipient> recipientRepo, IMapper mapper)
        {
            _recipientRepo = recipientRepo;
            _mapper = mapper;
        }

        public async Task<RecipientResponseDto> AddRecipientAsync(CreateRecipientDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new BadRequestException("Recipient name is required.");

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new BadRequestException("Recipient email is required.");

            if (!dto.Email.Contains("@") || !dto.Email.Contains("."))
                throw new BadRequestException("Please provide a valid email address.");

            // Check for duplicate email under same user
            var existing = await _recipientRepo.FindAsync(r => r.Email == dto.Email && r.CreatedByUserId == userId);
            if (existing.Any())
                throw new ConflictException($"A recipient with email '{dto.Email}' already exists.");

            var recipient = _mapper.Map<Recipient>(dto);
            recipient.CreatedByUserId = userId;
            recipient.CreatedAt = DateTime.UtcNow;

            await _recipientRepo.AddAsync(recipient);
            return _mapper.Map<RecipientResponseDto>(recipient);
        }

        public async Task<PagedResult<RecipientResponseDto>> GetAllRecipientsAsync(int userId, PaginationParams paginationParams)
        {
            if (paginationParams.PageNumber <= 0)
                throw new BadRequestException("Page number must be greater than 0.");
            if (paginationParams.PageSize <= 0 || paginationParams.PageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");

            var allRecipients = await _recipientRepo.FindAsync(r => r.CreatedByUserId == userId);
            var query = allRecipients.AsQueryable();

            // Filter by search term
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(r => r.Name.ToLower().Contains(search)
                    || r.Email.ToLower().Contains(search)
                    || (r.GroupName != null && r.GroupName.ToLower().Contains(search)));
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "name" => paginationParams.SortDescending ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
                "email" => paginationParams.SortDescending ? query.OrderByDescending(r => r.Email) : query.OrderBy(r => r.Email),
                "group" => paginationParams.SortDescending ? query.OrderByDescending(r => r.GroupName) : query.OrderBy(r => r.GroupName),
                _ => query.OrderByDescending(r => r.CreatedAt)
            };

            var totalCount = query.Count();

            var recipients = query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<RecipientResponseDto>
            {
                Items = _mapper.Map<List<RecipientResponseDto>>(recipients),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<PagedResult<RecipientResponseDto>> GetRecipientsByGroupAsync(string groupName, int userId, PaginationParams paginationParams)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new BadRequestException("Group name is required.");
            if (paginationParams.PageNumber <= 0)
                throw new BadRequestException("Page number must be greater than 0.");
            if (paginationParams.PageSize <= 0 || paginationParams.PageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");

            var allRecipients = await _recipientRepo.FindAsync(r => r.CreatedByUserId == userId && r.GroupName == groupName);
            var query = allRecipients.AsQueryable();

            // Filter
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(r => r.Name.ToLower().Contains(search) || r.Email.ToLower().Contains(search));
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "name" => paginationParams.SortDescending ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
                "email" => paginationParams.SortDescending ? query.OrderByDescending(r => r.Email) : query.OrderBy(r => r.Email),
                _ => query.OrderByDescending(r => r.CreatedAt)
            };

            var totalCount = query.Count();

            var recipients = query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<RecipientResponseDto>
            {
                Items = _mapper.Map<List<RecipientResponseDto>>(recipients),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<bool> DeleteRecipientAsync(int id)
        {
            if (id <= 0)
                throw new BadRequestException("Recipient ID must be a positive number.");

            var recipient = await _recipientRepo.GetByIdAsync(id);
            if (recipient == null)
                throw new NotFoundException($"Recipient with ID {id} was not found.");

            await _recipientRepo.DeleteAsync(recipient);
            return true;
        }

        public async Task<IEnumerable<RecipientResponseDto>> ImportRecipientsAsync(List<CreateRecipientDto> dtos, int userId)
        {
            if (dtos == null || !dtos.Any())
                throw new BadRequestException("At least one recipient is required for import.");

            // Validate all entries before importing
            for (int i = 0; i < dtos.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(dtos[i].Name))
                    throw new BadRequestException($"Recipient at row {i + 1}: Name is required.");
                if (string.IsNullOrWhiteSpace(dtos[i].Email))
                    throw new BadRequestException($"Recipient at row {i + 1}: Email is required.");
                if (!dtos[i].Email.Contains("@") || !dtos[i].Email.Contains("."))
                    throw new BadRequestException($"Recipient at row {i + 1}: Invalid email address '{dtos[i].Email}'.");
            }

            // Check for duplicates within the import list
            var duplicateEmails = dtos.GroupBy(d => d.Email.ToLower()).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateEmails.Any())
                throw new BadRequestException($"Duplicate emails found in import: {string.Join(", ", duplicateEmails)}.");

            var results = new List<RecipientResponseDto>();
            foreach (var dto in dtos)
            {
                var result = await AddRecipientAsync(dto, userId);
                results.Add(result);
            }
            return results;
        }
    }
}
