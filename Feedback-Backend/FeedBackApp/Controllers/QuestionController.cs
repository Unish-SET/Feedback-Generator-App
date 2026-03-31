using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/surveys/{surveyId}/questions")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _questionService;

        public QuestionController(IQuestionService questionService) => _questionService = questionService;

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        [HttpPost]
        public async Task<IActionResult> AddQuestion(int surveyId, [FromBody] CreateQuestionDto dto)
        {
            var result = await _questionService.AddQuestionAsync(surveyId, dto, GetUserId(), GetUserRole());
            return CreatedAtAction(nameof(GetQuestions), new { surveyId },
                new { success = true, data = result });
        }

        [HttpPut("{questionId}")]
        public async Task<IActionResult> UpdateQuestion(int surveyId, int questionId, [FromBody] UpdateQuestionDto dto)
        {
            var result = await _questionService.UpdateQuestionAsync(surveyId, questionId, dto, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        [HttpDelete("{questionId}")]
        public async Task<IActionResult> DeleteQuestion(int surveyId, int questionId)
        {
            await _questionService.DeleteQuestionAsync(surveyId, questionId, GetUserId(), GetUserRole());
            return Ok(new { success = true, message = "Question deleted successfully." });
        }

        [HttpGet]
        public async Task<IActionResult> GetQuestions(int surveyId, [FromQuery] string? type = null)
        {
            var result = await _questionService.GetQuestionsAsync(surveyId, GetUserId(), GetUserRole(), type);
            return Ok(new { success = true, data = result });
        }
    }
}
