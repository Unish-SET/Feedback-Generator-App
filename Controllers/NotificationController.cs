using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff,Viewer,Respondent")]  // all authenticated roles
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Any authenticated role — own notifications only
        [HttpGet]
        public async Task<ActionResult<PagedResult<NotificationResponseDto>>> GetMyNotifications([FromQuery] PaginationParams paginationParams)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, paginationParams);
            return Ok(notifications);
        }

        // Any authenticated role
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { count });
        }

        // Any authenticated role — mark own notification as read
        [HttpPut("{id}/mark-read")]
        public async Task<ActionResult> MarkAsRead(int id)
        {
            var result = await _notificationService.MarkAsReadAsync(id);
            if (!result) return NotFound();
            return Ok(new { message = "Notification marked as read." });
        }
    }
}
