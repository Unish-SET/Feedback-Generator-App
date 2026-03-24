using FeedBackApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Context
{
    /// <summary>
    /// Creates a brand-new FeedBackDbContext using its own connection string.
    /// Used exclusively by AuditService so audit writes never share a
    /// transaction with the main business request.
    /// </summary>
    public class AuditDbContextFactory : IAuditDbContextFactory
    {
        private readonly string _connectionString;

        public AuditDbContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public FeedBackDbContext Create()
        {
            var options = new DbContextOptionsBuilder<FeedBackDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            return new FeedBackDbContext(options);
        }
    }
}
