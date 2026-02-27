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

        // Admin, Staff can create distributions
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<DistributionResponseDto>> CreateDistribution([FromBody] CreateDistributionDto dto)
        {
            var result = await _distributionService.CreateDistributionAsync(dto);
            return Ok(result);
        }

        // Admin, Staff, Viewer can list distributions for a survey
        [HttpGet("survey/{surveyId}")]
        [Authorize(Roles = "Admin,Staff,Viewer")]
        public async Task<ActionResult<IEnumerable<DistributionResponseDto>>> GetDistributionsBySurvey(int surveyId)
        {
            var distributions = await _distributionService.GetDistributionsBySurveyAsync(surveyId);
            return Ok(distributions);
        }

        // Admin only can delete distributions
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteDistribution(int id)
        {
            var deleted = await _distributionService.DeleteDistributionAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
