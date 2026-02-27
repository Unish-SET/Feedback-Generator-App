using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        // Admin, Staff, Viewer can read analytics
        [HttpGet("survey/{surveyId}")]
        [Authorize(Roles = "Admin,Staff,Viewer")]
        public async Task<ActionResult<SurveyAnalyticsDto>> GetSurveyAnalytics(int surveyId)
        {
            var analytics = await _analyticsService.GetSurveyAnalyticsAsync(surveyId);
            if (analytics == null) return NotFound();
            return Ok(analytics);
        }
    }
}
