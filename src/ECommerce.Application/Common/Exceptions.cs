namespace ECommerce.Application.Common;

public class AppException : Exception
{
    public int StatusCode { get; }
    public IEnumerable<string>? Errors { get; }

    public AppException(string message, int statusCode = 400, IEnumerable<string>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string resource) : base($"{resource} tapılmadı.", 404) { }
}

public class ValidationException : AppException
{
    public ValidationException(IEnumerable<string> errors) : base("Validasiya xətası.", 422, errors) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Giriş icazəsi yoxdur.") : base(message, 401) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bu əməliyyat üçün icazəniz yoxdur.") : base(message, 403) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409) { }
}
