using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SurveyResponseController : ControllerBase
    {
        private readonly ISurveyResponseService _responseService;

        public SurveyResponseController(ISurveyResponseService responseService)
        {
            _responseService = responseService;
        }

        [HttpPost]
        public async Task<ActionResult<SurveyResponseDetailDto>> SubmitResponse([FromBody] SubmitResponseDto dto)
        {
            int? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
                userId = int.Parse(userIdClaim.Value);

            var result = await _responseService.SubmitResponseAsync(dto, userId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<SurveyResponseDetailDto>> GetResponse(int id)
        {
            var response = await _responseService.GetResponseByIdAsync(id);
            if (response == null) return NotFound();
            return Ok(response);
        }

        [HttpGet("survey/{surveyId}")]
        [Authorize]
        public async Task<ActionResult<PagedResult<SurveyResponseDetailDto>>> GetResponsesBySurvey(int surveyId, [FromQuery] PaginationParams paginationParams)
        {
            var responses = await _responseService.GetResponsesBySurveyAsync(surveyId, paginationParams);
            return Ok(responses);
        }

        [HttpPut("{id}/pause")]
        [Authorize]
        public async Task<ActionResult<SurveyResponseDetailDto>> PauseResponse(int id)
        {
            var result = await _responseService.PauseResponseAsync(id);
            return Ok(result);
        }

        [HttpPut("{id}/resume")]
        [Authorize]
        public async Task<ActionResult<SurveyResponseDetailDto>> ResumeResponse(int id, [FromBody] List<SubmitAnswerDto> additionalAnswers)
        {
            var result = await _responseService.ResumeResponseAsync(id, additionalAnswers);
            return Ok(result);
        }
    }
}
