using FeedBackApp.Context;
using FeedBackApp.Exceptions;
using FeedBackApp.Models;
using System.Security.Claims;

namespace FeedBackApp.Exceptions.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var statusCode = ex is AppException appEx ? appEx.StatusCode : 500;
            var message    = ex is AppException ? ex.Message : "An unexpected error occurred.";

            // Extract user info from JWT claims
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName    = context.User.Identity?.Name ?? "Anonymous";
            var role        = context.User.FindFirst(ClaimTypes.Role)?.Value ?? "Anonymous";

            var controller = context.Request.RouteValues["controller"]?.ToString() ?? string.Empty;
            var action     = context.Request.RouteValues["action"]?.ToString()     ?? string.Empty;

            // ILogger — structured logging
            _logger.LogError(ex,
                "Exception | Status:{StatusCode} | User:{User} | Role:{Role} | {Controller}/{Action} | {Message}",
                statusCode, userName, role, controller, action, message);

            // Persist to AuditLog table
            try
            {
                int? userId = int.TryParse(userIdClaim, out var uid) ? uid : null;

                using var scope = context.RequestServices.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<FeedBackDbContext>();

                await db.AuditLogs.AddAsync(new AuditLog
                {
                    Id            = Guid.NewGuid(),
                    Action        = $"Exception:{ex.GetType().Name}",
                    EntityName    = controller,
                    EntityId      = action,
                    UserId        = userId,
                    Changes       = $"Status:{statusCode} | {message}",
                    IPAddress     = context.Connection.RemoteIpAddress?.ToString(),
                    CorrelationId = context.Items["CorrelationId"]?.ToString(),
                    Timestamp     = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                _logger.LogCritical(logEx, "CRITICAL: Failed to persist exception log to database.");
            }

            // API Response
            if (context.Response.HasStarted) return;

            context.Response.StatusCode  = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                success    = false,
                statusCode,
                message,
                traceId    = context.TraceIdentifier
            });
        }
    }
}
