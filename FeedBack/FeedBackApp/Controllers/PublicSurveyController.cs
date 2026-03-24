using FeedBackApp.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FeedBackApp.Controllers
{
    /// <summary>Public survey endpoint — no authentication required.</summary>
    [Route("survey")]
    [ApiController]
    public class PublicSurveyController : ControllerBase
    {
        private readonly ISurveyService _surveyService;

        public PublicSurveyController(ISurveyService surveyService) => _surveyService = surveyService;

        /// <summary>Fetch a published survey by its public token (for respondents).</summary>
        [HttpGet("{publicToken}")]
        public async Task<IActionResult> GetByPublicToken(Guid publicToken)
        {
            var result = await _surveyService.GetByPublicTokenAsync(publicToken);
            return Ok(new { success = true, data = result });
        }
    }
}
