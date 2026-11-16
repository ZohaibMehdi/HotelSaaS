namespace HotelSaaS.Application.Common;

/// <summary>Base for expected, user-facing failures mapped to proper HTTP codes.</summary>
public abstract class AppException(string message, int statusCode, object? errors = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public object? Errors { get; } = errors;
}

public class BadRequestException(string message, object? errors = null) : AppException(message, 400, errors);
public class UnauthorizedException(string message = "Authentication required.") : AppException(message, 401);
public class ForbiddenException(string message = "You do not have permission to perform this action.") : AppException(message, 403);
public class NotFoundException(string message = "Resource not found.") : AppException(message, 404);
public class ConflictException(string message) : AppException(message, 409);

/// <summary>Maps to 422 Unprocessable Entity - business rule violation with valid syntax (PRD section 37).</summary>
public class BusinessRuleException(string message, object? errors = null) : AppException(message, 422, errors);

public class ValidationException(string message, object errors) : AppException(message, 400, errors);
