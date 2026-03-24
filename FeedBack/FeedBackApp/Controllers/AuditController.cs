using FeedBackApp.Context;
using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AuditController : ControllerBase
    {
        private readonly FeedBackDbContext _db;

        public AuditController(FeedBackDbContext db) => _db = db;

        /// <summary>
        /// GET /api/audit?page=1&pageSize=20&search=&action=&entity=
        /// Returns paginated audit logs with backend-controlled pagination.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? action = null,
            [FromQuery] string? entity = null)
        {
            // 🔥 BACKEND CONTROL (IMPORTANT)
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 20);

            var query = _db.AuditLogs
                .Join(_db.Users,
                    log => log.UserId,
                    user => user.Id,
                    (log, user) => new { log, username = user.Username })
                .Union(
                    _db.AuditLogs
                        .Where(log => log.UserId == null)
                        .Select(log => new { log, username = (string)"System" })
                )
                .AsQueryable();

            // 🔍 Filters
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x =>
                    x.log.Action.Contains(search) ||
                    x.log.EntityName.Contains(search) ||
                    (x.log.EntityId != null && x.log.EntityId.Contains(search)) ||
                    x.username.Contains(search));

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(x => x.log.Action == action);

            if (!string.IsNullOrWhiteSpace(entity))
                query = query.Where(x => x.log.EntityName == entity);

            // 📊 Total count (before pagination)
            var total = await query.CountAsync();

            // 📄 Pagination + sorting
            var items = await query
                .OrderByDescending(x => x.log.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AuditLogDto
                {
                    Id = x.log.Id,
                    UserId = x.log.UserId,
                    Username = x.username,
                    Action = x.log.Action,
                    EntityName = x.log.EntityName,
                    EntityId = x.log.EntityId,
                    Changes = x.log.Changes,
                    IPAddress = x.log.IPAddress,
                    CorrelationId = x.log.CorrelationId,
                    Timestamp = x.log.Timestamp
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    items,
                    totalCount = total,
                    pageNumber = page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)total / pageSize),
                    hasPrevious = page > 1,
                    hasNext = page < (int)Math.Ceiling((double)total / pageSize)
                }
            });
        }

        /// <summary>
        /// GET /api/audit/meta
        /// Returns distinct actions and entities for filters.
        /// </summary>
        [HttpGet("meta")]
        public async Task<IActionResult> GetMeta()
        {
            var actions = await _db.AuditLogs
                .Select(l => l.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var entities = await _db.AuditLogs
                .Select(l => l.EntityName)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new { actions, entities }
            });
        }
    }

   
}