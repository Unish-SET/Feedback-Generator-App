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

        public SurveyController(ISurveyService surveyService) => _surveyService = surveyService;

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams pagination)
        {
            var result = await _surveyService.GetAllAsync(pagination, GetUserId(), GetUserRole());
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

        [HttpPatch("{id}/publish")]
        public async Task<IActionResult> Publish(int id)
        {
            var result = await _surveyService.PublishAsync(id, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        [HttpPatch("{id}/unpublish")]
        public async Task<IActionResult> Unpublish(int id)
        {
            var result = await _surveyService.UnpublishAsync(id, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        [HttpPatch("{id}/close")]
        public async Task<IActionResult> Close(int id)
        {
            var result = await _surveyService.CloseAsync(id, GetUserId(), GetUserRole());
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
    
