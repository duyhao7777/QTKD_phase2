using Microsoft.EntityFrameworkCore;
using QTKD.Api.Infrastructure;

namespace QTKD.Tests.Unit;

/// <summary>
/// DbContext chạy trong bộ nhớ cho unit test. Mỗi lần gọi tạo một database riêng,
/// nên các test không ảnh hưởng lẫn nhau và không đụng đến PostgreSQL.
/// </summary>
public static class TestDb
{
    public static QtkdDbContext Create()
    {
        var options = new DbContextOptionsBuilder<QtkdDbContext>()
            .UseInMemoryDatabase($"qtkd-unit-{Guid.NewGuid():N}")
            .Options;

        var db = new QtkdDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
