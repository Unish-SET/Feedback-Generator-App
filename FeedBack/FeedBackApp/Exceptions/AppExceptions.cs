namespace FeedBackApp.Exceptions
{
    /// <summary>Base exception — all domain exceptions inherit from this.</summary>
    public class AppException : Exception
    {
        public int StatusCode { get; }

        public AppException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class NotFoundException : AppException
    {
        public NotFoundException(string message) : base(message, 404) { }
    }

    public class ConflictException : AppException
    {
        public ConflictException(string message) : base(message, 409) { }
    }

    public class BadRequestException : AppException
    {
        public BadRequestException(string message) : base(message, 400) { }
    }

    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message) : base(message, 403) { }
    }

    public class UnAuthorizedException : AppException
    {
        public UnAuthorizedException(string message = "Unauthorized") : base(message, 401) { }
    }
}
