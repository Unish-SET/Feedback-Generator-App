using AutoMapper;
using FeedBackGeneratorApp.DTOs;
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
            var recipient = _mapper.Map<Recipient>(dto);
            recipient.CreatedByUserId = userId;
            recipient.CreatedAt = DateTime.UtcNow;

            await _recipientRepo.AddAsync(recipient);
            return _mapper.Map<RecipientResponseDto>(recipient);
        }

        public async Task<PagedResult<RecipientResponseDto>> GetAllRecipientsAsync(int userId, PaginationParams paginationParams)
        {
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
            var recipient = await _recipientRepo.GetByIdAsync(id);
            if (recipient == null) return false;

            await _recipientRepo.DeleteAsync(recipient);
            return true;
        }

        public async Task<IEnumerable<RecipientResponseDto>> ImportRecipientsAsync(List<CreateRecipientDto> dtos, int userId)
        {
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
