using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SurveyController : ControllerBase
    {
        private readonly ISurveyService _surveyService;

        public SurveyController(ISurveyService surveyService)
        {
            _surveyService = surveyService;
        }

        // Admin, Staff can create surveys
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<SurveyResponseDto>> CreateSurvey([FromBody] CreateSurveyDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _surveyService.CreateSurveyAsync(dto, userId);
            return CreatedAtAction(nameof(GetSurvey), new { id = result.Id }, result);
        }

        // Public — anyone can browse surveys
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<SurveyResponseDto>>> GetAllSurveys([FromQuery] PaginationParams paginationParams)
        {
            var surveys = await _surveyService.GetAllSurveysAsync(paginationParams);
            return Ok(surveys);
        }

        // Public — anyone can view a survey
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<SurveyResponseDto>> GetSurvey(int id)
        {
            var survey = await _surveyService.GetSurveyByIdAsync(id);
            if (survey == null) return NotFound();
            return Ok(survey);
        }

        // Admin, Staff, Viewer can see their own surveys
        [HttpGet("my-surveys")]
        [Authorize(Roles = "Admin,Staff,Viewer")]
        public async Task<ActionResult<PagedResult<SurveyResponseDto>>> GetMySurveys([FromQuery] PaginationParams paginationParams)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var surveys = await _surveyService.GetSurveysByUserAsync(userId, paginationParams);
            return Ok(surveys);
        }

        // Admin, Staff can update surveys
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<SurveyResponseDto>> UpdateSurvey(int id, [FromBody] UpdateSurveyDto dto)
        {
            var result = await _surveyService.UpdateSurveyAsync(id, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }

        // Admin only can delete surveys
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteSurvey(int id)
        {
            var deleted = await _surveyService.DeleteSurveyAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }

        // Admin, Staff can add questions
        [HttpPost("{surveyId}/questions")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<QuestionResponseDto>> AddQuestion(int surveyId, [FromBody] CreateQuestionDto dto)
        {
            var result = await _surveyService.AddQuestionAsync(surveyId, dto);
            return Ok(result);
        }

        // Admin only can delete questions
        [HttpDelete("questions/{questionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteQuestion(int questionId)
        {
            var deleted = await _surveyService.DeleteQuestionAsync(questionId);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
