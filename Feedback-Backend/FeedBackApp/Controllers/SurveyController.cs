using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class SurveyController : ControllerBase
    {
        private readonly ISurveyService _surveyService;

        public SurveyController(ISurveyService surveyService)
        {
            _surveyService = surveyService;
        }

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] SurveyFilterParams filter)
        {
            var result = await _surveyService.GetAllAsync(filter, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _surveyService.GetByIdAsync(id, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSurveyDto dto)
        {
            var result = await _surveyService.CreateAsync(dto, GetUserId());
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new { success = true, data = result });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSurveyDto dto)
        {
            var result = await _surveyService.UpdateAsync(id, dto, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// Transition survey state.
        /// Inactive → Active (Publish), Active → Inactive (Pause), Active/Inactive → Closed (permanent).
        /// Closed surveys cannot be reopened.
        /// </summary>
        [HttpPatch("{id}/state")]
        public async Task<IActionResult> SetState(int id, [FromBody] SetSurveyStateDto dto)
        {
            var result = await _surveyService.SetStateAsync(id, dto, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// Update survey schedule (startDate / endDate) regardless of state.
        /// Works on Inactive, Active, and Closed surveys.
        /// </summary>
        [HttpPatch("{id}/schedule")]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] UpdateSurveyScheduleDto dto)
        {
            var result = await _surveyService.UpdateScheduleAsync(id, dto, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _surveyService.DeleteAsync(id, GetUserId(), GetUserRole());
            return Ok(new { success = true, message = "Survey deleted successfully." });
        }

        [HttpPost("{sourceId}/clone-questions/{targetId}")]
        public async Task<IActionResult> CloneQuestions(int sourceId, int targetId)
        {
            await _surveyService.CloneQuestionsAsync(sourceId, targetId, GetUserId(), GetUserRole());
            return Ok(new { success = true, message = "Questions cloned successfully." });
        }
    }
}
