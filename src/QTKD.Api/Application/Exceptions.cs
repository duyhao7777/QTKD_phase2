namespace QTKD.Api.Application;

public record ErrorItem(string Code, string Message);

public abstract class AppException(string code, string message, IReadOnlyList<ErrorItem>? errors = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyList<ErrorItem> Errors { get; } = errors ?? Array.Empty<ErrorItem>();
    public abstract int StatusCode { get; }
}

public class ValidationFailedException(string code, string message, IReadOnlyList<ErrorItem>? errors = null)
    : AppException(code, message, errors)
{
    public override int StatusCode => 400;

    public static void ThrowIfAny(List<ErrorItem> errors)
    {
        if (errors.Count > 0)
            throw new ValidationFailedException("VALIDATION_FAILED", "Dữ liệu không hợp lệ", errors);
    }
}

public class ForbiddenException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => 403;
}

public class NotFoundException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => 404;
}

public class ConflictException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => 409;
}