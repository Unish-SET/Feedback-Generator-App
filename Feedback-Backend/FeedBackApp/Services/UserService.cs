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
    public class UserService : IUserService
    {
        private readonly IRepository<User>   _userRepo;
        private readonly IRepository<Survey> _surveyRepo;
        private readonly IAuditService       _audit;

        public UserService(
            IRepository<User>   userRepo,
            IRepository<Survey> surveyRepo,
            IAuditService       audit)
        {
            _userRepo   = userRepo;
            _surveyRepo = surveyRepo;
            _audit      = audit;
        }

        public async Task<PaginatedResult<UserResponseDto>> GetAllUsersAsync(UserFilterParams filter)
        {
            // Validate date range before hitting the DB
            if (filter.FromDate.HasValue && filter.ToDate.HasValue &&
                filter.FromDate.Value > filter.ToDate.Value)
                throw new BadRequestException("FromDate cannot be later than ToDate.");

            var query = _userRepo.GetQueryable().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(u =>
                    u.Username.Contains(filter.Search) ||
                    u.Email.Contains(filter.Search));

            if (!string.IsNullOrWhiteSpace(filter.Role) &&
                Enum.TryParse<UserRole>(filter.Role, true, out var roleEnum))
                query = query.Where(u => u.Role == roleEnum);

            // Date range — normalize to UTC start/end of day so partial dates work intuitively
            if (filter.FromDate.HasValue)
            {
                var from = DateTime.SpecifyKind(filter.FromDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(u => u.CreatedAt >= from);
            }

            if (filter.ToDate.HasValue)
            {
                // End of day: include everything up to 23:59:59.999 on ToDate
                var to = DateTime.SpecifyKind(filter.ToDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(u => u.CreatedAt <= to);
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.Username)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(u => new UserResponseDto
                {
                    Id            = u.Id,
                    Username      = u.Username,
                    Email         = u.Email,
                    Role          = u.Role.ToString(),
                    IsActive      = u.IsActive,
                    IsDeleted     = u.IsDeleted,
                    CreatedAt     = u.CreatedAt,
                    SurveyCount   = u.Surveys.Count(s => !s.IsDeleted),
                    ResponseCount = u.Responses.Count()
                })
                .ToListAsync();

            return new PaginatedResult<UserResponseDto>
            {
                Items      = users,
                PageNumber = filter.PageNumber,
                PageSize   = filter.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<UserResponseDto> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.GetQueryable()
                .Where(u => u.Id == id)
                .Select(u => new UserResponseDto
                {
                    Id            = u.Id,
                    Username      = u.Username,
                    Email         = u.Email,
                    Role          = u.Role.ToString(),
                    IsActive      = u.IsActive,
                    IsDeleted     = u.IsDeleted,
                    CreatedAt     = u.CreatedAt,
                    SurveyCount   = u.Surveys.Count(s => !s.IsDeleted),
                    ResponseCount = u.Responses.Count()
                })
                .FirstOrDefaultAsync();

            if (user == null)
                throw new NotFoundException($"User with ID {id} not found.");

            return user;
        }

        public async Task<UserResponseDto> UpdateUserRoleAsync(int id, UpdateUserRoleDto dto)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException($"User with ID {id} not found.");

            if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
                throw new BadRequestException($"Invalid role '{dto.Role}'. Valid roles: Admin, Creator, Respondent.");

            user.Role = role;
            _userRepo.Update(user);
            await _userRepo.SaveChangesAsync();
            _ = _audit.LogAsync("UpdateRole", "User", id.ToString(),
                newValues: $"{{\"role\":\"{dto.Role}\"}}");
            return await GetUserByIdAsync(id);
        }

        public async Task<UserResponseDto> SetUserStatusAsync(int id, UpdateUserStatusDto dto)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException($"User with ID {id} not found.");

            if (user.IsDeleted)
                throw new BadRequestException("Cannot change status of a deleted user.");

            if (user.IsActive == dto.IsActive)
                throw new BadRequestException($"User is already {(dto.IsActive ? "active" : "inactive")}.");

            user.IsActive = dto.IsActive;
            _userRepo.Update(user);
            await _userRepo.SaveChangesAsync();

            _ = _audit.LogAsync(
                action:    dto.IsActive ? "Activate" : "Deactivate",
                entityName: "User",
                entityId:  id.ToString());

            return await GetUserByIdAsync(id);
        }

        public async Task SoftDeleteUserAsync(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException($"User with ID {id} not found.");

            if (user.IsDeleted)
                throw new BadRequestException("User is already deleted.");

            // Prevent deleting a user who owns active surveys.
            // Deleting them would leave live surveys with no accessible owner —
            // creators can no longer manage them and responses keep coming in.
            var hasActiveSurveys = await _surveyRepo.GetQueryable()
                .AnyAsync(s => s.CreatedBy == id && s.State == SurveyState.Active);

            if (hasActiveSurveys)
                throw new BadRequestException(
                    "Cannot delete a user who has active surveys. " +
                    "Close or reassign their active surveys first.");

            user.IsDeleted = true;
            user.IsActive  = false;
            _userRepo.Update(user);
            await _userRepo.SaveChangesAsync();
            _ = _audit.LogAsync("SoftDelete", "User", id.ToString());
        }

        public async Task<PaginatedResult<SurveyListDto>> GetSurveysByUserAsync(
            int userId, int pageNumber, int pageSize)
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize   = Math.Min(pageSize <= 0 ? 20 : pageSize, 50);

            // IgnoreQueryFilters to include soft-deleted surveys in admin view
            var query = _surveyRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(s => s.CreatedBy == userId);

            var totalCount = await query.CountAsync();

            var surveys = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SurveyListDto
                {
                    Id            = s.Id,
                    Title         = s.Title,
                    State         = s.State.ToString(),
                    PublicToken   = s.PublicToken.ToString(),
                    ResponseCount = s.Responses.Count(),
                    CreatedAt     = s.CreatedAt,
                    CreatedBy     = s.CreatedBy,
                    CreatorName   = s.Creator.Username
                })
                .ToListAsync();

            return new PaginatedResult<SurveyListDto>
            {
                Items      = surveys,
                PageNumber = pageNumber,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }
    }
}
