using AutoMapper;
using FeedBackGeneratorApp.DTOs;
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
            var notification = await _notificationRepo.GetByIdAsync(notificationId);
            if (notification == null) return false;

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
