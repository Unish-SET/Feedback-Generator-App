using FeedBackApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Context
{
    
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
