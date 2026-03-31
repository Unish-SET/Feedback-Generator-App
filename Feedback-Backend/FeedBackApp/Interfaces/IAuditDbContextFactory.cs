using FeedBackApp.Context;

namespace FeedBackApp.Interfaces
{
    /// <summary>
    /// Creates independent FeedBackDbContext instances for AuditService.
    /// Registered as Singleton — avoids conflicts with the scoped DbContext registration.
    /// </summary>
    public interface IAuditDbContextFactory
    {
        FeedBackDbContext Create();
    }
}
