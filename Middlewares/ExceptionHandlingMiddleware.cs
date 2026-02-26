using System.Net;
using System.Text.Json;

namespace FeedBackGeneratorApp.Middlewares
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
                InvalidOperationException => HttpStatusCode.BadRequest,           // 400
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,       // 401
                KeyNotFoundException => HttpStatusCode.NotFound,                  // 404
                ArgumentException => HttpStatusCode.BadRequest,                   // 400
                _ => HttpStatusCode.InternalServerError                           // 500
            };

            var response = new
            {
                statusCode = (int)statusCode,
                message = exception.Message,
                details = statusCode == HttpStatusCode.InternalServerError
                    ? "An internal server error occurred. Please try again later."
                    : exception.Message
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
        }
    }
}
