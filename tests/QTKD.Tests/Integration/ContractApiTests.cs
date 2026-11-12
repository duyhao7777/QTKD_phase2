using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Domain;
using Xunit;

namespace QTKD.Tests.Integration;

public class ContractApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    // TC03: sửa Contract ở Draft -> thành công khi dữ liệu hợp lệ
    [Fact]
    public async Task TC03_UpdateDraftContract_ReturnsOk_AndIsPersisted()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId);

        var res = await UpdateContractAsync(contractId, customerId, "Dịch vụ cảng", "Nội dung mới");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.Equal("Nội dung mới", body.GetProperty("content").GetString());
        Assert.Equal("Draft", body.GetProperty("status").GetString());

        var saved = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contractId));
        Assert.Equal("Nội dung mới", saved.Content);
        Assert.Equal("Dịch vụ cảng", saved.ServiceGroup);
        Assert.Equal(ContractStatus.Draft, saved.Status);
    }

    // TC04: sửa Contract ở PendingApproval -> bị từ chối, dữ liệu giữ nguyên (CTR-01)
    [Fact]
    public async Task TC04_UpdateContractInPendingApproval_ReturnsConflict_AndDataUnchanged()
    {
        var (customerId, contractId, _) = await CreateSubmittedContractAsync();

        var res = await UpdateContractAsync(contractId, customerId, "Nhóm bị sửa trái phép", "Nội dung bị sửa trái phép");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.Equal("CTR-01", body.GetProperty("code").GetString());

        var saved = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contractId));
        Assert.Equal("Nội dung hợp lệ", saved.Content);
        Assert.Equal("Dịch vụ cảng", saved.ServiceGroup);
        Assert.Equal(ContractStatus.PendingApproval, saved.Status);
    }

    // Bổ sung: tạo hợp đồng luôn ở Draft và đọc lại được
    [Fact]
    public async Task CreateContract_StartsAsDraft_AndCanBeReadBack()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId);

        var get = await Client.GetAsync($"/api/contracts/{contractId}");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await BodyAsync(get);
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal(customerId, body.GetProperty("customerId").GetGuid());
    }

    // Bổ sung: không cho trùng số hợp đồng
    [Fact]
    public async Task CreateContractWithDuplicateNumber_ReturnsConflict()
    {
        var customerId = await CreateCustomerAsync();
        var contractNo = $"HD-DUP-{Guid.NewGuid():N}";
        object Payload() => new
        {
            contractNo,
            customerId,
            startDate = new DateOnly(2026, 10, 1),
            endDate = new DateOnly(2027, 9, 30)
        };

        var first = await Client.PostAsJsonAsync("/api/contracts", Payload());
        var second = await Client.PostAsJsonAsync("/api/contracts", Payload());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("CONTRACT_NO_DUPLICATE", (await BodyAsync(second)).GetProperty("code").GetString());
        Assert.Equal(1, await WithDbAsync(db => db.Contracts.CountAsync(c => c.ContractNo == contractNo)));
    }

    // Bổ sung: tạo hợp đồng cho khách hàng không tồn tại
    [Fact]
    public async Task CreateContractForUnknownCustomer_ReturnsBadRequest()
    {
        var res = await Client.PostAsJsonAsync("/api/contracts", new
        {
            contractNo = $"HD-X-{Guid.NewGuid():N}",
            customerId = Guid.NewGuid(),
            startDate = new DateOnly(2026, 10, 1),
            endDate = new DateOnly(2027, 9, 30)
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("CUSTOMER_NOT_FOUND", (await BodyAsync(res)).GetProperty("code").GetString());
    }

    // Bổ sung: ngày bắt đầu lớn hơn ngày kết thúc
    [Fact]
    public async Task CreateContractWithStartAfterEnd_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/contracts", new
        {
            contractNo = $"HD-D-{Guid.NewGuid():N}",
            customerId,
            startDate = new DateOnly(2027, 10, 1),
            endDate = new DateOnly(2026, 9, 30)
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("PERIOD_INVALID", ErrorCodes(await BodyAsync(res)));
    }

    // Bổ sung: hợp đồng không tồn tại
    [Fact]
    public async Task GetUnknownContract_ReturnsNotFound()
    {
        var res = await Client.GetAsync($"/api/contracts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("CONTRACT_NOT_FOUND", (await BodyAsync(res)).GetProperty("code").GetString());
    }

    // TC10: body sai định dạng (customerId không phải GUID) phải trả lỗi theo MẪU THỐNG NHẤT
    // { code, message, errors } như đã mô tả trong docs/api.md.
    [Fact]
    public async Task TC10_MalformedBody_ReturnsUnifiedErrorFormat()
    {
        var json = """
            { "contractNo": "HD-BAD", "customerId": "khong-phai-guid",
              "startDate": "2026-10-01", "endDate": "2027-09-30" }
            """;

        var res = await Client.PostAsync("/api/contracts",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.True(body.TryGetProperty("code", out var code), "Response lỗi thiếu trường 'code' theo mẫu thống nhất");
        Assert.Equal("INVALID_REQUEST", code.GetString());
        Assert.True(body.TryGetProperty("errors", out _), "Response lỗi thiếu trường 'errors'");
    }
}
