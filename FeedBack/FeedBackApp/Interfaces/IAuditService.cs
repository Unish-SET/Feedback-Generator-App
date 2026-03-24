namespace FeedBackApp.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(
            string  action,
            string  entityName,
            string? entityId,
            int?    userId        = null,
            string? oldValues     = null,
            string? newValues     = null,
            string? ipAddress     = null,
            string? correlationId = null);
    }
}
