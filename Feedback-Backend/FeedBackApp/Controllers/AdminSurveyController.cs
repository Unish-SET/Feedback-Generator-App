using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/admin/surveys")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminSurveyController : ControllerBase
    {
        private readonly IAdminSurveyService _adminSurveyService;

        public AdminSurveyController(IAdminSurveyService adminSurveyService)
            => _adminSurveyService = adminSurveyService;

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetIp()       => HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        private string GetCorrelId() => HttpContext.Items["CorrelationId"]?.ToString() ?? string.Empty;

        /// <summary>GET /api/admin/surveys — all surveys with pagination, search, filters.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AdminSurveyFilterParams filter)
        {
            var result = await _adminSurveyService.GetAllSurveysAsync(filter);
            return Ok(new { success = true, data = result });
        }

        /// <summary>GET /api/admin/surveys/stats — TotalSurveys, ActiveSurveys, DeletedSurveys, TotalResponses.</summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var result = await _adminSurveyService.GetStatsAsync();
            return Ok(new { success = true, data = result });
        }

        /// <summary>GET /api/admin/surveys/{id} — full survey detail including all versions.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            try
            {
                var result = await _adminSurveyService.GetSurveyDetailAsync(id);
                return Ok(new { success = true, data = result });
            }
            catch (NotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        }

        /// <summary>
        /// PATCH /api/admin/surveys/{id}/state
        /// Admin can force any state: Inactive, Active, Closed.
        /// Body: { "state": "Active" }
        /// ALIGN-02: renamed from /status → /state and body field "status" → "state"
        /// to match the domain model and the regular SurveyController endpoint.
        /// </summary>
        [HttpPatch("{id}/state")]
        public async Task<IActionResult> SetState(int id, [FromBody] SetSurveyStateDto dto)
        {
            try
            {
                await _adminSurveyService.SetSurveyStatusAsync(id, dto.State, GetUserId());
                return Ok(new { success = true, message = $"Survey state updated to {dto.State}." });
            }
            catch (NotFoundException ex)   { return NotFound(new { success = false, message = ex.Message }); }
            catch (BadRequestException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        /// <summary>PATCH /api/admin/surveys/{id}/restore — undo soft-delete.</summary>
        [HttpPatch("{id}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                await _adminSurveyService.RestoreSurveyAsync(id, GetUserId(), GetIp(), GetCorrelId());
                return Ok(new { success = true, message = $"Survey {id} has been restored." });
            }
            catch (NotFoundException ex)   { return NotFound(new { success = false, message = ex.Message }); }
            catch (BadRequestException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        /// <summary>DELETE /api/admin/surveys/{id} — soft-delete with audit log.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            try
            {
                await _adminSurveyService.SoftDeleteSurveyAsync(id, GetUserId(), GetIp(), GetCorrelId());
                return Ok(new { success = true, message = $"Survey {id} has been deleted." });
            }
            catch (NotFoundException ex)   { return NotFound(new { success = false, message = ex.Message }); }
            catch (BadRequestException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }
    }
}
