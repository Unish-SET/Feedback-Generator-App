using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedBackApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService) => _userService = userService;

        /// <summary>GET /api/user — list all users with search, role filter, pagination.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] UserFilterParams filter)
        {
            var result = await _userService.GetAllUsersAsync(filter);
            return Ok(new { success = true, data = result });
        }

        /// <summary>GET /api/user/{id} — user detail with survey/response counts.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _userService.GetUserByIdAsync(id);
            return Ok(new { success = true, data = result });
        }

        /// <summary>GET /api/user/{id}/surveys — all surveys created by this user (admin view, includes deleted).</summary>
        [HttpGet("{id}/surveys")]
        public async Task<IActionResult> GetSurveys(int id,
            [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _userService.GetSurveysByUserAsync(id, pageNumber, pageSize);
            return Ok(new { success = true, data = result });
        }

        /// <summary>PATCH /api/user/{id}/role — change user role.</summary>
        [HttpPatch("{id}/role")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateUserRoleDto dto)
        {
            var result = await _userService.UpdateUserRoleAsync(id, dto);
            return Ok(new { success = true, data = result });
        }

        /// <summary>PATCH /api/user/{id}/status — activate or deactivate a user.</summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> SetStatus(int id, [FromBody] UpdateUserStatusDto dto)
        {
            var result = await _userService.SetUserStatusAsync(id, dto);
            return Ok(new { success = true, data = result });
        }

        /// <summary>DELETE /api/user/{id} — soft-delete a user.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _userService.SoftDeleteUserAsync(id);
            return Ok(new { success = true, message = "User deleted successfully." });
        }
    }
}
