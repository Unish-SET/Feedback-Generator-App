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
    public class RecipientController : ControllerBase
    {
        private readonly IRecipientService _recipientService;

        public RecipientController(IRecipientService recipientService)
        {
            _recipientService = recipientService;
        }

        // Admin, Staff can add recipients
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<RecipientResponseDto>> AddRecipient([FromBody] CreateRecipientDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _recipientService.AddRecipientAsync(dto, userId);
            return Ok(result);
        }

        // Admin, Staff, Viewer can list recipients
        [HttpGet]
        [Authorize(Roles = "Admin,Staff,Viewer")]
        public async Task<ActionResult<PagedResult<RecipientResponseDto>>> GetAllRecipients([FromQuery] PaginationParams paginationParams)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var recipients = await _recipientService.GetAllRecipientsAsync(userId, paginationParams);
            return Ok(recipients);
        }

        // Admin, Staff, Viewer can filter by group
        [HttpGet("group/{groupName}")]
        [Authorize(Roles = "Admin,Staff,Viewer")]
        public async Task<ActionResult<PagedResult<RecipientResponseDto>>> GetRecipientsByGroup(string groupName, [FromQuery] PaginationParams paginationParams)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var recipients = await _recipientService.GetRecipientsByGroupAsync(groupName, userId, paginationParams);
            return Ok(recipients);
        }

        // Admin, Staff can delete recipients
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult> DeleteRecipient(int id)
        {
            var deleted = await _recipientService.DeleteRecipientAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }

        // Admin, Staff can bulk import
        [HttpPost("import")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<RecipientResponseDto>>> ImportRecipients([FromBody] List<CreateRecipientDto> dtos)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var results = await _recipientService.ImportRecipientsAsync(dtos, userId);
            return Ok(results);
        }
    }
}
