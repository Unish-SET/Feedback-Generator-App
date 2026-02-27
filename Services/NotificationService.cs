using AutoMapper;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IRepository<Notification> _notificationRepo;
        private readonly IMapper _mapper;

        public NotificationService(IRepository<Notification> notificationRepo, IMapper mapper)
        {
            _notificationRepo = notificationRepo;
            _mapper = mapper;
        }

        public async Task<NotificationResponseDto> CreateNotificationAsync(int userId, string message)
        {
            if (userId <= 0)
                throw new BadRequestException("User ID must be a positive number.");
            if (string.IsNullOrWhiteSpace(message))
                throw new BadRequestException("Notification message cannot be empty.");

            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepo.AddAsync(notification);
            return _mapper.Map<NotificationResponseDto>(notification);
        }

        public async Task<PagedResult<NotificationResponseDto>> GetUserNotificationsAsync(int userId, PaginationParams paginationParams)
        {
            if (paginationParams.PageNumber <= 0)
                throw new BadRequestException("Page number must be greater than 0.");
            if (paginationParams.PageSize <= 0 || paginationParams.PageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");

            var allNotifications = await _notificationRepo.FindAsync(n => n.UserId == userId);
            var query = allNotifications.AsQueryable();

            // Filter by read/unread
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                if (search == "unread")
                    query = query.Where(n => !n.IsRead);
                else if (search == "read")
                    query = query.Where(n => n.IsRead);
                else
                    query = query.Where(n => n.Message.ToLower().Contains(search));
            }

            // Sort
            query = query.OrderByDescending(n => n.CreatedAt);

            var totalCount = query.Count();

            var notifications = query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<NotificationResponseDto>
            {
                Items = _mapper.Map<List<NotificationResponseDto>>(notifications),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<bool> MarkAsReadAsync(int notificationId)
        {
            if (notificationId <= 0)
                throw new BadRequestException("Notification ID must be a positive number.");

            var notification = await _notificationRepo.GetByIdAsync(notificationId);
            if (notification == null)
                throw new NotFoundException($"Notification with ID {notificationId} was not found.");

            if (notification.IsRead)
                throw new BadRequestException("This notification is already marked as read.");

            notification.IsRead = true;
            await _notificationRepo.UpdateAsync(notification);
            return true;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            var notifications = await _notificationRepo.FindAsync(n => n.UserId == userId && !n.IsRead);
            return notifications.Count();
        }
    }
}
