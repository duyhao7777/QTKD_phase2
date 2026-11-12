using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QTKD.Api.Infrastructure;
using Xunit;

namespace QTKD.Tests.Integration;

/// <summary>
/// Tất cả integration test dùng chung MỘT ApiFactory (một database kiểm thử)
/// và chạy tuần tự trong cùng collection "Api".
/// Mỗi test tự tạo dữ liệu riêng với tên/số hợp đồng ngẫu nhiên nên không phụ thuộc test khác.
/// </summary>
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
}

[Collection("Api")]
public abstract class ApiTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;
    protected HttpClient Client { get; } = factory.CreateClient();

    // ---------- Đọc response ----------

    protected static async Task<JsonElement> BodyAsync(HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>();

    protected static List<string?> ErrorCodes(JsonElement body) =>
        body.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .ToList();

    // ---------- Đọc database ----------

    protected async Task<T> WithDbAsync<T>(Func<QtkdDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QtkdDbContext>();
        return await action(db);
    }

    // ---------- Tạo dữ liệu qua API ----------

    protected async Task<Guid> CreateCustomerAsync(string? name = null)
    {
        var res = await Client.PostAsJsonAsync("/api/customers",
            new { name = name ?? $"KH-{Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await BodyAsync(res)).GetProperty("id").GetGuid();
    }

    protected async Task<Guid> CreateDraftContractAsync(Guid customerId, bool complete = false)
    {
        var res = await Client.PostAsJsonAsync("/api/contracts", new
        {
            contractNo = $"HD-T-{Guid.NewGuid():N}",
            customerId,
            startDate = new DateOnly(2026, 10, 1),
            endDate = new DateOnly(2027, 9, 30),
            serviceGroup = complete ? "Dịch vụ cảng" : null,
            content = complete ? "Nội dung hợp lệ" : null
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await BodyAsync(res)).GetProperty("id").GetGuid();
    }

    protected Task<HttpResponseMessage> UpdateContractAsync(
        Guid id, Guid customerId, string? serviceGroup, string? content) =>
        Client.PutAsJsonAsync($"/api/contracts/{id}", new
        {
            customerId,
            startDate = new DateOnly(2026, 10, 1),
            endDate = new DateOnly(2027, 9, 30),
            serviceGroup,
            content
        });

    protected Task<HttpResponseMessage> SubmitAsync(
        Guid contractId, string? key = null, string role = "Sales", string userId = "sales01")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/contracts/{contractId}/submit")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-User-Id", userId);
        request.Headers.Add("X-User-Role", role);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return Client.SendAsync(request);
    }

    /// <summary>Tạo Customer + Contract đầy đủ rồi gửi trình duyệt thành công.</summary>
    protected async Task<(Guid CustomerId, Guid ContractId, Guid ApprovalId)> CreateSubmittedContractAsync()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var res = await SubmitAsync(contractId, key: $"k-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var approvalId = (await BodyAsync(res)).GetProperty("approvalId").GetGuid();
        return (customerId, contractId, approvalId);
    }
}
