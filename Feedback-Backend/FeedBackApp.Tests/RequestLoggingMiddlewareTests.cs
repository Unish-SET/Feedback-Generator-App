using FeedBackApp.Exceptions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeedBackApp.Tests
{
    [TestFixture]
    public class RequestLoggingMiddlewareTests
    {
        // ── Correlation ID — generation ───────────────────────────────────────

        [Test]
        public async Task Invoke_NoIncomingCorrelationId_GeneratesOne()
        {
            var context = MakeContext("/api/test");
            var mw = MakeMiddleware();

            await mw.InvokeAsync(context);

            Assert.That(context.Items["CorrelationId"], Is.Not.Null);
            Assert.That(context.Items["CorrelationId"]!.ToString(), Is.Not.Empty);
        }

        [Test]
        public async Task Invoke_IncomingCorrelationId_ReusesThatValue()
        {
            var context = MakeContext("/api/test");
            context.Request.Headers["X-Correlation-ID"] = "my-fixed-id";
            var mw = MakeMiddleware();

            await mw.InvokeAsync(context);

            Assert.That(context.Items["CorrelationId"]?.ToString(), Is.EqualTo("my-fixed-id"));
        }

        [Test]
        public async Task Invoke_CorrelationId_AppendedToResponseHeader()
        {
            var context = MakeContext("/api/test");
            context.Request.Headers["X-Correlation-ID"] = "resp-check";
            var mw = MakeMiddleware();

            await mw.InvokeAsync(context);

            Assert.That(
                context.Response.Headers["X-Correlation-ID"].ToString(),
                Is.EqualTo("resp-check"));
        }

        [Test]
        public async Task Invoke_GeneratedCorrelationId_MatchesBetweenItemAndResponseHeader()
        {
            var context = MakeContext("/api/test");
            var mw = MakeMiddleware();

            await mw.InvokeAsync(context);

            var itemCorr = context.Items["CorrelationId"]?.ToString();
            var responseCorr = context.Response.Headers["X-Correlation-ID"].ToString();

            Assert.That(responseCorr, Is.Not.Empty);
            Assert.That(responseCorr, Is.EqualTo(itemCorr));
        }

        // ── Pipeline passthrough ──────────────────────────────────────────────

        [Test]
        public async Task Invoke_AlwaysCallsNextMiddleware()
        {
            var nextCalled = false;
            var context = MakeContext("/api/test");
            var mw = new RequestLoggingMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                NullLogger<RequestLoggingMiddleware>.Instance);

            await mw.InvokeAsync(context);

            Assert.That(nextCalled, Is.True);
        }

        [Test]
        public async Task Invoke_SkipPath_StillCallsNext()
        {
            var nextCalled = false;
            var context = MakeContext("/health");
            var mw = new RequestLoggingMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                NullLogger<RequestLoggingMiddleware>.Instance);

            await mw.InvokeAsync(context);

            Assert.That(nextCalled, Is.True);
        }

        [Test]
        public async Task Invoke_SkipPath_StillSetsCorrelationIdForDownstream()
        {
            // CorrelationId is set on ALL requests (including skip paths)
            // so downstream middleware like ExcelController and AuditService always have it.
            // Skip paths only skip LOGGING — not correlation ID injection.
            var context = MakeContext("/health");
            var mw = MakeMiddleware();

            await mw.InvokeAsync(context);

            Assert.That(context.Items.ContainsKey("CorrelationId"), Is.True);
            Assert.That(context.Items["CorrelationId"]?.ToString(), Is.Not.Empty);
        }

        // ── Exception propagation ─────────────────────────────────────────────

        [Test]
        public void Invoke_NextThrows_ExceptionBubblesUp()
        {
            var context = MakeContext("/api/test");
            var mw = new RequestLoggingMiddleware(
                _ => throw new InvalidOperationException("boom"),
                NullLogger<RequestLoggingMiddleware>.Instance);

            Assert.ThrowsAsync<InvalidOperationException>(
                () => mw.InvokeAsync(context));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static DefaultHttpContext MakeContext(string path)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "GET";
            ctx.Request.Path = path;
            return ctx;
        }

        private static RequestLoggingMiddleware MakeMiddleware() =>
            new RequestLoggingMiddleware(
                _ => Task.CompletedTask,
                NullLogger<RequestLoggingMiddleware>.Instance);
    }
}