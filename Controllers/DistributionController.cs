using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DistributionController : ControllerBase
    {
        private readonly IDistributionService _distributionService;

        public DistributionController(IDistributionService distributionService)
        {
            _distributionService = distributionService;
        }

        [HttpPost]
        public async Task<ActionResult<DistributionResponseDto>> CreateDistribution([FromBody] CreateDistributionDto dto)
        {
            var result = await _distributionService.CreateDistributionAsync(dto);
            return Ok(result);
        }

        [HttpGet("survey/{surveyId}")]
        public async Task<ActionResult<IEnumerable<DistributionResponseDto>>> GetDistributionsBySurvey(int surveyId)
        {
            var distributions = await _distributionService.GetDistributionsBySurveyAsync(surveyId);
            return Ok(distributions);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteDistribution(int id)
        {
            var deleted = await _distributionService.DeleteDistributionAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
