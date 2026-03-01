using System.Net;
using System.Text.Json;

namespace FeedBackGeneratorApp.Exceptions
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
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
                _logger.LogError(ex, "An unhandled exception occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = exception switch
            {
                // Custom Application Exceptions (your business rules)
                ApiException apiEx => apiEx.StatusCode,

                // Database Exceptions (EF Core)
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => HttpStatusCode.Conflict,       // 409
                Microsoft.EntityFrameworkCore.DbUpdateException             => HttpStatusCode.BadRequest,     // 400

                // Bad Data / Parsing Exceptions
                ArgumentNullException      => HttpStatusCode.BadRequest,      // 400
                ArgumentException          => HttpStatusCode.BadRequest,      // 400
                InvalidOperationException  => HttpStatusCode.BadRequest,      // 400
                FormatException            => HttpStatusCode.BadRequest,      // 400

                // Authorization / Access
                UnauthorizedAccessException => HttpStatusCode.Forbidden,      // 403
                KeyNotFoundException        => HttpStatusCode.NotFound,       // 404

                // System / Network Constraints
                TimeoutException           => HttpStatusCode.RequestTimeout,  // 408
                NotImplementedException    => HttpStatusCode.NotImplemented,  // 501

                // Fallback — catch everything else
                _ => HttpStatusCode.InternalServerError                       // 500
            };

            // For custom ApiException, the message is developer-controlled and safe to return.
            // For all other unhandled .NET exceptions, return a generic message to avoid
            // leaking internal details (stack traces, SQL errors, etc.) to clients.
            var isApiException = exception is ApiException;
            var userMessage = isApiException
                ? exception.Message
                : "An unexpected error occurred. Please try again later.";

            var response = new
            {
                statusCode = (int)statusCode,
                message = userMessage,
                details = statusCode == HttpStatusCode.InternalServerError
                    ? "An internal server error occurred. Please try again later."
                    : userMessage
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
        }
    }
}
