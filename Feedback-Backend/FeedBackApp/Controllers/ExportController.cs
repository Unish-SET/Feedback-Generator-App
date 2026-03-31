using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/surveys/{surveyId}/export")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class ExportController : ControllerBase
    {
        private readonly IExcelService            _excelService;
        private readonly IAuditService            _auditService;
        private readonly ILogger<ExportController> _logger;

        public ExportController(
            IExcelService excelService,
            IAuditService auditService,
            ILogger<ExportController> logger)
        {
            _excelService = excelService;
            _auditService = auditService;
            _logger       = logger;
        }

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        /// <summary>Export survey responses as formatted Excel (.xlsx).</summary>
        [HttpGet("excel")]
        public async Task<IActionResult> ExportExcel(int surveyId)
        {
            var userId        = GetUserId();
            var role          = GetUserRole();
            var correlationId = HttpContext.Items["CorrelationId"]?.ToString();
            var ip            = HttpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                _logger.LogInformation(
                    "[EXCEL] Export requested Survey={SurveyId} User={UserId} Role={Role}",
                    surveyId, userId, role);

                var bytes = await _excelService.ExportExcelAsync(surveyId, userId, role);

                _logger.LogInformation(
                    "[EXCEL] Export succeeded Survey={SurveyId} Size={Bytes}B User={UserId}",
                    surveyId, bytes.Length, userId);

                _ = _auditService.LogAsync(
                    action:        "ExcelExport",
                    entityName:    "Survey",
                    entityId:      surveyId.ToString(),
                    userId:        userId,
                    ipAddress:     ip,
                    correlationId: correlationId);

                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"survey_{surveyId}_responses.xlsx");
            }
            catch (NotFoundException ex)   { return NotFound(new { success = false, message = ex.Message }); }
            catch (ForbiddenException ex)  { return StatusCode(403, new { success = false, message = ex.Message }); }
            catch (BadRequestException ex) { return BadRequest(new { success = false, message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EXCEL] Unexpected error Survey={SurveyId}", surveyId);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred." });
            }
        }
    }
}
