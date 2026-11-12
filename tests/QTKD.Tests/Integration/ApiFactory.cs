using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QTKD.Api.Infrastructure;

namespace QTKD.Tests.Integration;

/// <summary>
/// Khởi động toàn bộ API trong bộ nhớ (TestServer) nhưng nối vào database PostgreSQL KIỂM THỬ RIÊNG.
/// Database này bị xóa và tạo lại bằng migration mỗi lần chạy bộ test,
/// nên tên database bắt buộc phải chứa chữ "test".
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        var connectionString = ResolveTestConnectionString();

        // Ghi đè chuỗi kết nối của API. Biến môi trường được đọc sau user-secrets nên thắng.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);

        // Tạo lại database kiểm thử bằng chính các migration của dự án.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QtkdDbContext>();
        db.Database.EnsureDeleted();
        db.Database.Migrate();
    }

    private static string ResolveTestConnectionString()
    {
        // Ưu tiên biến môi trường (hợp cho máy khác / CI), sau đó đến user-secrets.
        var connectionString = Environment.GetEnvironmentVariable("QTKD_TEST_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = new ConfigurationBuilder()
                .AddUserSecrets<ApiFactory>(optional: true)
                .Build()["ConnectionStrings:Test"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình database kiểm thử. Đặt user-secrets 'ConnectionStrings:Test' " +
                "(dotnet user-secrets set ... --project tests/QTKD.Tests) " +
                "hoặc biến môi trường QTKD_TEST_CONNECTION. Xem README.");
        }

        var database = new NpgsqlConnectionStringBuilder(connectionString).Database ?? string.Empty;
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database '{database}' không phải database kiểm thử (tên phải chứa 'test'). " +
                "Từ chối chạy để tránh xóa nhầm dữ liệu thật.");
        }

        return connectionString;
    }
}
