using FeedBackApp.Context;
using FeedBackApp.Interfaces;
using FeedBackApp.Models;
using FeedBackApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeedBackApp.Tests
{
    [TestFixture]
    public class AuditServiceTests
    {
        private FeedBackDbContext _db;
        private AuditService      _auditService;

        [SetUp]
        public void Setup()
        {
            var dbName  = $"AuditTest_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<FeedBackDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            _db = new FeedBackDbContext(options);

            // Use the in-memory factory that matches IAuditDbContextFactory
            var factory   = new InMemoryAuditDbContextFactory(options);
            _auditService = new AuditService(factory, NullLogger<AuditService>.Instance);
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        // ── Basic write ───────────────────────────────────────────────────────

        [Test]
        public async Task LogAsync_ValidCall_CreatesOneRow()
        {
            await _auditService.LogAsync("Create", "Survey", "42", userId: 1);

            var logs = await _db.AuditLogs.ToListAsync();
            Assert.That(logs.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task LogAsync_SetsActionCorrectly()
        {
            await _auditService.LogAsync("Delete", "Survey", "7");

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.Action, Is.EqualTo("Delete"));
        }

        [Test]
        public async Task LogAsync_SetsEntityNameAndEntityId()
        {
            await _auditService.LogAsync("Update", "SurveyResponse", "123");

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.EntityName, Is.EqualTo("SurveyResponse"));
            Assert.That(log.EntityId,   Is.EqualTo("123"));
        }

        [Test]
        public async Task LogAsync_SetsUserIdCorrectly()
        {
            await _auditService.LogAsync("Create", "User", "5", userId: 99);

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.UserId, Is.EqualTo(99));
        }

        // ── Nullable fields ───────────────────────────────────────────────────

        [Test]
        public async Task LogAsync_NullUserId_StillPersists()
        {
            await _auditService.LogAsync("Create", "SurveyResponse", "1", userId: null);

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.UserId, Is.Null);
        }

        [Test]
        public async Task LogAsync_NullEntityId_StillPersists()
        {
            await _auditService.LogAsync("ExcelExport", "Survey", entityId: null, userId: 1);

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.EntityId, Is.Null);
        }

        // ── Changes JSON ──────────────────────────────────────────────────────

        [Test]
        public async Task LogAsync_WithOldAndNewValues_ChangesIsNotNull()
        {
            await _auditService.LogAsync("Update", "Survey", "10",
                oldValues: "{\"Title\":\"Old\"}", newValues: "{\"Title\":\"New\"}");

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.Changes, Is.Not.Null);
        }

        [Test]
        public async Task LogAsync_WithOldAndNewValues_ChangesContainsBothKeys()
        {
            await _auditService.LogAsync("Update", "Survey", "10",
                oldValues: "{\"Title\":\"Old\"}", newValues: "{\"Title\":\"New\"}");

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.Changes, Does.Contain("OldValues"));
            Assert.That(log.Changes, Does.Contain("NewValues"));
        }

        [Test]
        public async Task LogAsync_NoOldOrNewValues_ChangesIsNull()
        {
            await _auditService.LogAsync("Create", "Survey", "1");

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.Changes, Is.Null);
        }

        // ── IP + CorrelationId ────────────────────────────────────────────────

        [Test]
        public async Task LogAsync_IPAddressPersisted()
        {
            await _auditService.LogAsync("Create", "Survey", "1", ipAddress: "192.168.1.100");

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.IPAddress, Is.EqualTo("192.168.1.100"));
        }

        [Test]
        public async Task LogAsync_CorrelationIdPersisted()
        {
            var corrId = Guid.NewGuid().ToString("N");
            await _auditService.LogAsync("Create", "Survey", "1", correlationId: corrId);

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.CorrelationId, Is.EqualTo(corrId));
        }

        // ── Timestamp ─────────────────────────────────────────────────────────

        [Test]
        public async Task LogAsync_TimestampIsApproximatelyNow()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            await _auditService.LogAsync("Create", "Survey", "1");
            var after = DateTime.UtcNow.AddSeconds(1);

            var log = await _db.AuditLogs.SingleAsync();
            Assert.That(log.Timestamp, Is.InRange(before, after));
        }

        // ── Multiple rows ─────────────────────────────────────────────────────

        [Test]
        public async Task LogAsync_CalledThreeTimes_ThreeRowsExist()
        {
            await _auditService.LogAsync("Create", "Survey", "1");
            await _auditService.LogAsync("Update", "Survey", "1");
            await _auditService.LogAsync("Delete", "Survey", "1");

            Assert.That(await _db.AuditLogs.CountAsync(), Is.EqualTo(3));
        }

        [Test]
        public async Task LogAsync_MultipleRows_EachHasDistinctGuid()
        {
            await _auditService.LogAsync("Create", "Survey", "1");
            await _auditService.LogAsync("Update", "Survey", "1");

            var ids = await _db.AuditLogs.Select(l => l.Id).ToListAsync();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(2));
        }

        // ── Resilience ────────────────────────────────────────────────────────

        [Test]
        public void LogAsync_InvalidJsonInOldValues_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(() =>
                _auditService.LogAsync("Update", "Survey", "1",
                    oldValues: "NOT_VALID_JSON",
                    newValues: "{\"Title\":\"OK\"}"));
        }

        // ── In-memory factory helper ──────────────────────────────────────────

        private sealed class InMemoryAuditDbContextFactory : IAuditDbContextFactory
        {
            private readonly DbContextOptions<FeedBackDbContext> _opts;
            public InMemoryAuditDbContextFactory(DbContextOptions<FeedBackDbContext> opts) => _opts = opts;
            public FeedBackDbContext Create() => new FeedBackDbContext(_opts);
        }
    }
}
