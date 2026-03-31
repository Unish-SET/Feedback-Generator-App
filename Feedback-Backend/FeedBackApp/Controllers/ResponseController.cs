using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/surveys")]
    [ApiController]
    public class ResponseController : ControllerBase
    {
        private readonly IResponseService _responseService;

        public ResponseController(IResponseService responseService)
        {
            _responseService = responseService;
        }

        /// <summary>Submit a response — anonymous or authenticated.</summary>
        [HttpPost("{publicToken}/responses")]
        [AllowAnonymous]
        [EnableRateLimiting("survey-submit")]
        public async Task<IActionResult> Submit(Guid publicToken, [FromBody] SubmitResponseDto dto)
        {
            int? userId = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out var parsed))
                    userId = parsed;
            }

            var result = await _responseService.SubmitAsync(publicToken, dto, userId);
            return StatusCode(201, new { success = true, data = result });
        }

        /// <summary>Get all responses for a survey (Admin/Creator only).</summary>
        [HttpGet("{surveyId}/responses")]
        [Authorize(Roles = "Admin,Creator")]
        public async Task<IActionResult> GetResponses(int surveyId, [FromQuery] ResponseFilterParams filter)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized(new { success = false, message = "Unauthorized." });

            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

            var result = await _responseService.GetResponsesAsync(surveyId, filter, userId, role);
            return Ok(new { success = true, data = result });
        }
    }
}
