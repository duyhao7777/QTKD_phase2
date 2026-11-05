using Microsoft.AspNetCore.Diagnostics;
using QTKD.Api.Application;

namespace QTKD.Api.ErrorHandling;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is AppException app)
        {
            ctx.Response.StatusCode = app.StatusCode;
            await ctx.Response.WriteAsJsonAsync(
                new { code = app.Code, message = app.Message, errors = app.Errors }, ct);
            return true;
        }

        logger.LogError(ex, "Lỗi không xử lý được");
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(
            new { code = "INTERNAL_ERROR", message = "Lỗi hệ thống", errors = Array.Empty<object>() }, ct);
        return true;
    }
}