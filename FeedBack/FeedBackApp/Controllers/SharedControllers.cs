using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    // ── ANALYTICS ─────────────────────────────────────────────────────────────
    [Route("api/surveys/{surveyId}/analytics")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService) => _analyticsService = analyticsService;

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        [HttpGet]
        public async Task<IActionResult> GetAnalytics(int surveyId)
        {
            try
            {
                var result = await _analyticsService.GetAnalyticsAsync(surveyId, GetUserId(), GetUserRole());
                return Ok(new { success = true, data = result });
            }
            catch (NotFoundException ex)   { return NotFound(new { success = false, message = ex.Message }); }
            catch (ForbiddenException ex)  { return StatusCode(403, new { success = false, message = ex.Message }); }
            catch (BadRequestException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }
    }

    // ── CSV EXPORT ────────────────────────────────────────────────────────────
    [Route("api/surveys/{surveyId}/export")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class ExportController : ControllerBase
    {
        private readonly IExportService _exportService;

        public ExportController(IExportService exportService) => _exportService = exportService;

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        /// <summary>Export survey responses as CSV.</summary>
        [HttpGet("csv")]
        public async Task<IActionResult> ExportCsv(int surveyId)
        {
            try
            {
                var bytes = await _exportService.ExportCsvAsync(surveyId, GetUserId(), GetUserRole());
                return File(bytes, "text/csv", $"survey_{surveyId}_export.csv");
            }
            catch (NotFoundException ex)   { return NotFound(new { success = false, message = ex.Message }); }
            catch (ForbiddenException ex)  { return StatusCode(403, new { success = false, message = ex.Message }); }
            catch (BadRequestException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }
    }

    // ── EXCEL EXPORT ──────────────────────────────────────────────────────────
    [Route("api/surveys/{surveyId}/export")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class ExcelController : ControllerBase
    {
        private readonly IExcelService  _excelService;
        private readonly IAuditService  _auditService;
        private readonly ILogger<ExcelController> _logger;

        public ExcelController(IExcelService excelService, IAuditService auditService,
            ILogger<ExcelController> logger)
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
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized(new { success = false, message = "Unauthorized." });

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
            catch (NotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EXCEL] Unexpected error Survey={SurveyId}", surveyId);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred." });
            }
        }
    }

    // ── USER MANAGEMENT (Admin only) ──────────────────────────────────────────
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService) => _userService = userService;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _userService.GetAllUsersAsync();
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _userService.GetUserByIdAsync(id);
            return Ok(new { success = true, data = result });
        }

        [HttpPatch("{id}/role")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] Models.DTOs.UpdateUserRoleDto dto)
        {
            var result = await _userService.UpdateUserRoleAsync(id, dto);
            return Ok(new { success = true, data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _userService.DeleteUserAsync(id);
            return Ok(new { success = true, message = "User deleted successfully." });
        }
    }
}
