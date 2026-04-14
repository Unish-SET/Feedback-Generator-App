using FeedBackApp.Context;
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

       
        [HttpGet]
        public async Task<IActionResult> GetLogs([FromQuery] AuditFilterParams filter)
        {
           
            var page     = filter.PageNumber;
            var pageSize = filter.PageSize;

         
            var query = _db.AuditLogs
                .GroupJoin(
                    _db.Users.IgnoreQueryFilters(),
                    log  => log.UserId,
                    user => (int?)user.Id,
                    (log, users) => new { log, users })
                .SelectMany(
                    x => x.users.DefaultIfEmpty(),
                    (x, user) => new { x.log, Username = user != null ? user.Username : "System" })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(x =>
                    x.log.Action.Contains(filter.Search)     ||
                    x.log.EntityName.Contains(filter.Search) ||
                    (x.log.EntityId != null && x.log.EntityId.Contains(filter.Search)) ||
                    x.Username.Contains(filter.Search));

            if (!string.IsNullOrWhiteSpace(filter.Action))
                query = query.Where(x => x.log.Action == filter.Action);

            if (!string.IsNullOrWhiteSpace(filter.Entity))
                query = query.Where(x => x.log.EntityName == filter.Entity);

            if (filter.UserId.HasValue)
                query = query.Where(x => x.log.UserId == filter.UserId.Value);

            if (filter.FromDate.HasValue)
                query = query.Where(x => x.log.Timestamp >= filter.FromDate.Value.ToUniversalTime());

            if (filter.ToDate.HasValue)
                query = query.Where(x => x.log.Timestamp <= filter.ToDate.Value.ToUniversalTime());

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.log.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AuditLogDto
                {
                    Id            = x.log.Id,
                    UserId        = x.log.UserId,
                    Username      = x.Username,
                    Action        = x.log.Action,
                    EntityName    = x.log.EntityName,
                    EntityId      = x.log.EntityId,
                    Changes       = x.log.Changes,
                    IPAddress     = x.log.IPAddress,
                    CorrelationId = x.log.CorrelationId,
                    Timestamp     = x.log.Timestamp
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)total / pageSize);

            return Ok(new
            {
                success = true,
                data = new
                {
                    items,
                    totalCount  = total,
                    pageNumber  = page,
                    pageSize,
                    totalPages,
                    hasPrevious = page > 1,
                    hasNext     = page < totalPages
                }
            });
        }

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

            return Ok(new { success = true, data = new { actions, entities } });
        }
    }
}
