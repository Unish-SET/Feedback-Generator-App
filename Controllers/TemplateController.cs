using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TemplateController : ControllerBase
    {
        private readonly ITemplateService _templateService;

        public TemplateController(ITemplateService templateService)
        {
            _templateService = templateService;
        }

        [HttpPost]
        public async Task<ActionResult<TemplateResponseDto>> CreateTemplate([FromBody] CreateTemplateDto dto)
        {
            var result = await _templateService.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetTemplate), new { id = result.Id }, result);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<TemplateResponseDto>>> GetAllTemplates()
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            return Ok(templates);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<TemplateResponseDto>> GetTemplate(int id)
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null) return NotFound();
            return Ok(template);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTemplate(int id)
        {
            var deleted = await _templateService.DeleteTemplateAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
