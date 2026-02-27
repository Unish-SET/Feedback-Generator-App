using System.Net;

namespace FeedBackGeneratorApp.Exceptions
{
    public class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public ApiException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class NotFoundException : ApiException
    {
        public NotFoundException(string message = "The requested resource was not found.")
            : base(message, HttpStatusCode.NotFound) { }
    }

    public class BadRequestException : ApiException
    {
        public BadRequestException(string message = "Invalid request.")
            : base(message, HttpStatusCode.BadRequest) { }
    }

    public class UnauthorizedException : ApiException
    {
        public UnauthorizedException(string message = "You are not authorized to perform this action.")
            : base(message, HttpStatusCode.Unauthorized) { }
    }

    public class ConflictException : ApiException
    {
        public ConflictException(string message = "A conflict occurred.")
            : base(message, HttpStatusCode.Conflict) { }
    }
}
