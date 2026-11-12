using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Domain;
using Xunit;

namespace QTKD.Tests.Integration;

public class CustomerApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    // TC01: tạo Customer hợp lệ -> tạo được và đọc lại từ DB
    [Fact]
    public async Task TC01_CreateValidCustomer_ReturnsCreated_AndIsPersisted()
    {
        var name = $"Công ty TC01 {Guid.NewGuid():N}";

        var res = await Client.PostAsJsonAsync("/api/customers", new { name });

        // HTTP status + nội dung trả về
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await BodyAsync(res);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal("Active", body.GetProperty("status").GetString());

        // Đọc lại qua API
        var get = await Client.GetAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        // Đọc lại trực tiếp từ database
        var saved = await WithDbAsync(db => db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id));
        Assert.NotNull(saved);
        Assert.Equal(name, saved!.Name);
        Assert.Equal(CustomerStatus.Active, saved.Status);
    }

    // TC02: tạo Customer thiếu thông tin bắt buộc -> bị từ chối, có thông báo lỗi, không lưu gì
    [Fact]
    public async Task TC02_CreateCustomerWithoutName_ReturnsBadRequest_AndNothingIsSaved()
    {
        var before = await WithDbAsync(db => db.Customers.CountAsync());

        var res = await Client.PostAsJsonAsync("/api/customers", new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        Assert.Contains("CUSTOMER_NAME_REQUIRED", ErrorCodes(body));

        var after = await WithDbAsync(db => db.Customers.CountAsync());
        Assert.Equal(before, after);
    }

    // Bổ sung: không cho tạo trùng tên khách hàng
    [Fact]
    public async Task CreateCustomerWithDuplicateName_ReturnsConflict()
    {
        var name = $"Công ty trùng {Guid.NewGuid():N}";
        await CreateCustomerAsync(name);

        var res = await Client.PostAsJsonAsync("/api/customers", new { name = name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.Equal("CUSTOMER_NAME_DUPLICATE", body.GetProperty("code").GetString());

        var count = await WithDbAsync(db =>
            db.Customers.CountAsync(c => c.Name.ToLower() == name.ToLower()));
        Assert.Equal(1, count);
    }

    // Bổ sung: đọc khách hàng không tồn tại
    [Fact]
    public async Task GetUnknownCustomer_ReturnsNotFound()
    {
        var res = await Client.GetAsync($"/api/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.Equal("CUSTOMER_NOT_FOUND", body.GetProperty("code").GetString());
    }
}
