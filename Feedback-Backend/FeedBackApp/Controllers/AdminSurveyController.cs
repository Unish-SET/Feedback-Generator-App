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

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AdminSurveyFilterParams filter)
        {
            var result = await _adminSurveyService.GetAllSurveysAsync(filter);
            return Ok(new { success = true, data = result });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var result = await _adminSurveyService.GetStatsAsync();
            return Ok(new { success = true, data = result });
        }

       
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
