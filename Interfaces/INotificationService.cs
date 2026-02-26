using FeedBackGeneratorApp.DTOs;

namespace FeedBackGeneratorApp.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationResponseDto> CreateNotificationAsync(int userId, string message);
        Task<PagedResult<NotificationResponseDto>> GetUserNotificationsAsync(int userId, PaginationParams paginationParams);
        Task<bool> MarkAsReadAsync(int notificationId);
        Task<int> GetUnreadCountAsync(int userId);
    }
}
