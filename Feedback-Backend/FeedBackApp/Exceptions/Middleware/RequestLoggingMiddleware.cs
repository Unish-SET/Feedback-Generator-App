using System.Diagnostics;

namespace FeedBackApp.Exceptions.Middleware
{
    /// <summary>
    /// Logs every HTTP request and response (method, path, status, duration).
    /// Generates or propagates X-Correlation-ID for distributed tracing.
    /// Must be registered BEFORE GlobalExceptionMiddleware.
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate                   _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        private static readonly HashSet<string> _skipPaths =
            new(StringComparer.OrdinalIgnoreCase)
            { "/health", "/favicon.ico", "/swagger/index.html" };

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // ── Correlation ID ────────────────────────────────────────────────
            var correlationId =
                context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? Guid.NewGuid().ToString("N");

            context.Items["CorrelationId"]               = correlationId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;

            if (_skipPaths.Contains(context.Request.Path.Value ?? string.Empty))
            {
                await _next(context);
                return;
            }

            // ── Log request ───────────────────────────────────────────────────
            var sw = Stopwatch.StartNew();
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            _logger.LogInformation(
                "[REQ ] {Method} {Path}{Query} from {IP} CorrelationId={CorrelationId}",
                context.Request.Method, context.Request.Path,
                context.Request.QueryString, ip, correlationId);

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var status = context.Response.StatusCode;
                var level  = status >= 500 ? LogLevel.Error
                           : status >= 400 ? LogLevel.Warning
                           : LogLevel.Information;

                _logger.Log(level,
                    "[RESP] {Method} {Path} -> {Status} in {Ms}ms CorrelationId={CorrelationId}",
                    context.Request.Method, context.Request.Path,
                    status, sw.ElapsedMilliseconds, correlationId);
            }
        }
    }
}
