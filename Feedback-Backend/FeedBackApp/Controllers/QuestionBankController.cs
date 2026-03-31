using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/question-bank")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class QuestionBankController : ControllerBase
    {
        private readonly IQuestionBankService _bankService;

        public QuestionBankController(IQuestionBankService bankService)
            => _bankService = bankService;

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        /// <summary>GET /api/question-bank — list bank questions with optional filters.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] BankQuestionFilterParams filter)
        {
            var result = await _bankService.GetAllAsync(filter, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        /// <summary>GET /api/question-bank/{id}</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _bankService.GetByIdAsync(id, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        /// <summary>POST /api/question-bank — add a question to the bank.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBankQuestionDto dto)
        {
            var result = await _bankService.CreateAsync(dto, GetUserId());
            return StatusCode(201, new { success = true, data = result });
        }

        /// <summary>PUT /api/question-bank/{id} — update a bank question.</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBankQuestionDto dto)
        {
            var result = await _bankService.UpdateAsync(id, dto, GetUserId(), GetUserRole());
            return Ok(new { success = true, data = result });
        }

        /// <summary>DELETE /api/question-bank/{id} — soft-delete a bank question.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _bankService.DeleteAsync(id, GetUserId(), GetUserRole());
            return Ok(new { success = true, message = "Bank question deleted." });
        }

        /// <summary>
        /// POST /api/question-bank/clone-into-survey
        /// Clone one or more bank questions into a Draft survey.
        /// </summary>
        [HttpPost("clone-into-survey")]
        public async Task<IActionResult> CloneIntoSurvey([FromBody] CloneFromBankDto dto)
        {
            var result = await _bankService.CloneIntoSurveyAsync(dto, GetUserId(), GetUserRole());
            return StatusCode(201, new { success = true, data = result });
        }
    }
}
