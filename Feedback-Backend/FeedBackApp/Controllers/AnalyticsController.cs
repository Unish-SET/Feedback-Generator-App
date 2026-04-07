using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/surveys/{surveyId}/analytics")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IEmailService     _emailService;

        public AnalyticsController(IAnalyticsService analyticsService, IEmailService emailService)
        {
            _analyticsService = analyticsService;
            _emailService     = emailService;
        }

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        [HttpGet]
        public async Task<IActionResult> GetAnalytics(int surveyId, [FromQuery] AnalyticsFilterParams filter)
        {
            var result = await _analyticsService.GetAnalyticsAsync(surveyId, GetUserId(), GetUserRole(), filter);
            return Ok(new { success = true, data = result });
        }

        [HttpPost("send-report")]
        public async Task<IActionResult> SendReport(int surveyId, [FromBody] SendAnalyticsReportDto dto)
        {
            var (html, title) = await _analyticsService.BuildReportHtmlAsync(surveyId, GetUserId(), GetUserRole());
            await _emailService.SendAnalyticsReportAsync(dto.RecipientEmail, html, title);
            return Ok(new { success = true, message = "Report sent." });
        }
    }
}
