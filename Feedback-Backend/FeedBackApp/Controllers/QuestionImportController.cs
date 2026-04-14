using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedBackApp.Controllers
{
    [Route("api/questions")]
    [ApiController]
    [Authorize(Roles = "Admin,Creator")]
    public class QuestionImportController : ControllerBase
    {
        private readonly IQuestionImportService _importService;

        public QuestionImportController(IQuestionImportService importService)
            => _importService = importService;

        private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".xls"
        };

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // .xlsx
            "application/vnd.ms-excel",                                           // .xls
            "application/octet-stream"                                            // some browsers send this for Excel
        };

        
        [HttpPost("import-excel")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB cap
        public async Task<IActionResult> ImportExcel(
            IFormFile file,
            [FromQuery] int?  surveyId          = null,
            [FromQuery] bool  addToQuestionBank = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded." });

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                return BadRequest(new
                {
                    success = false,
                    message = $"Invalid file type '{ext}'. Only .xlsx and .xls files are accepted."
                });

            if (!AllowedMimeTypes.Contains(file.ContentType))
                return BadRequest(new
                {
                    success = false,
                    message = $"Invalid content type '{file.ContentType}'. Upload a valid Excel file."
                });

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _importService.ImportAsync(
                    stream, surveyId, addToQuestionBank, GetUserId(), GetUserRole());

                return Ok(new { success = true, data = result });
            }
            catch (BadRequestException ex) { return BadRequest(new { success = false, message = ex.Message }); }
            catch (NotFoundException   ex) { return NotFound(new { success = false, message = ex.Message }); }
            catch (ForbiddenException  ex) { return StatusCode(403, new { success = false, message = ex.Message }); }
        }

       
        [HttpGet("import-template")]
        public IActionResult GetTemplate()
        {
            var bytes = _importService.GetTemplate();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "question-import-template.xlsx");
        }
    }
}
