namespace QTKD.Api.Application;

/// Giả lập người dùng qua header, chưa có đăng nhập thật.
public record UserContext(string UserId, string Role)
{
    public bool CanSubmitContract => Role.Equals("Sales", StringComparison.OrdinalIgnoreCase);

    public static UserContext From(HttpRequest req) => new(
        req.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous",
        req.Headers["X-User-Role"].FirstOrDefault() ?? "");
}