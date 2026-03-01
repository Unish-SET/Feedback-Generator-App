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
        private readonly IExportService _exportService;

        public AnalyticsController(IAnalyticsService analyticsService, IExportService exportService)
        {
            _analyticsService = analyticsService;
            _exportService = exportService;
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

        [HttpGet("survey/{surveyId}/export/csv")]
        [Authorize(Roles = "Admin,Staff,Viewer")]
        public async Task<IActionResult> ExportToCsv(int surveyId)
        {
            var analytics = await _analyticsService.GetSurveyAnalyticsAsync(surveyId);
            if (analytics == null) return NotFound();

            var csvBytes = _exportService.ExportAnalyticsToCsv(analytics);
            return File(csvBytes, "text/csv", $"SurveyAnalytics_{surveyId}.csv");
        }

        [HttpGet("survey/{surveyId}/export/excel")]
        [Authorize(Roles = "Admin,Staff,Viewer")]
        public async Task<IActionResult> ExportToExcel(int surveyId)
        {
            var analytics = await _analyticsService.GetSurveyAnalyticsAsync(surveyId);
            if (analytics == null) return NotFound();

            var excelBytes = _exportService.ExportAnalyticsToExcel(analytics);
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SurveyAnalytics_{surveyId}.xlsx");
        }
    }
}
