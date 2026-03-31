using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models;
using System.Text.Json;

namespace FeedBackApp.Services
{
    /// <summary>
    /// Writes AuditLog rows using its own independent DB connection via
    /// IAuditDbContextFactory — so audit writes never share a transaction
    /// with the business operation, and a failed audit never crashes the caller.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly IAuditDbContextFactory _dbFactory;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IAuditDbContextFactory dbFactory, ILogger<AuditService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task LogAsync(
            string action,
            string entityName,
            string? entityId,
            int? userId = null,
            string? oldValues = null,
            string? newValues = null,
            string? ipAddress = null,
            string? correlationId = null)
        {
            try
            {
                string? changes = null;
                if (oldValues != null || newValues != null)
                {
                    changes = JsonSerializer.Serialize(new
                    {
                        OldValues = oldValues is null ? (object?)null : TryParseJson(oldValues),
                        NewValues = newValues is null ? (object?)null : TryParseJson(newValues)
                    });
                }

                var log = new AuditLog
                {
                    Action = action,
                    EntityName = entityName,
                    EntityId = entityId,
                    UserId = userId,
                    Changes = changes,
                    IPAddress = ipAddress,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                };

                // Independent context — never shares a UoW with the caller
                await using var db = _dbFactory.Create();
                db.AuditLogs.Add(log);
                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "[AUDIT] {Action} {EntityName} {EntityId} by User={UserId} CorrelationId={CorrelationId}",
                    action, entityName, entityId ?? "N/A",
                    userId?.ToString() ?? "anonymous", correlationId ?? "-");
            }
            catch (Exception ex)
            {
                // Audit must NEVER crash the main request
                _logger.LogWarning(ex,
                    "[AUDIT] Failed to write audit log for {Action} on {EntityName} {EntityId}",
                    action, entityName, entityId ?? "N/A");
            }
        }

        private static object TryParseJson(string json)
        {
            try { return JsonSerializer.Deserialize<JsonElement>(json); }
            catch { return json; }
        }
    }
}


