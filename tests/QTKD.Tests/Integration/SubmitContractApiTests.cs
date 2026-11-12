using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Domain;
using Xunit;

namespace QTKD.Tests.Integration;

/// <summary>US03: POST /api/contracts/{id}/submit</summary>
public class SubmitContractApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    // TC05: submit Contract thiếu điều kiện CTR-02 -> 400, vẫn là Draft, không tạo phiên phê duyệt
    [Fact]
    public async Task TC05_SubmitContractMissingCtr02Conditions_ReturnsBadRequest_AndStaysDraft()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: false);

        var res = await SubmitAsync(contractId, key: "tc05");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.Equal("CTR-02", body.GetProperty("code").GetString());
        var codes = ErrorCodes(body);
        Assert.Contains("CTR-02.CONTENT_MISSING", codes);
        Assert.Contains("CTR-02.SERVICE_GROUP_MISSING", codes);

        var saved = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contractId));
        Assert.Equal(ContractStatus.Draft, saved.Status);
        Assert.Equal(0, await WithDbAsync(db => db.ApprovalInstances.CountAsync(a => a.ContractId == contractId)));
        Assert.Equal(0, await WithDbAsync(db => db.ContractHistories.CountAsync(h => h.ContractId == contractId)));
    }

    // TC06: submit Contract hợp lệ -> chuyển sang PendingApproval
    [Fact]
    public async Task TC06_SubmitValidContract_ReturnsOk_AndMovesToPendingApproval()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var res = await SubmitAsync(contractId, key: "tc06");

        // HTTP status + nội dung trả về
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await BodyAsync(res);
        Assert.Equal(contractId, body.GetProperty("contractId").GetGuid());
        Assert.Equal("PendingApproval", body.GetProperty("status").GetString());
        var approvalId = body.GetProperty("approvalId").GetGuid();

        // Dữ liệu sau khi gọi API
        var contract = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contractId));
        Assert.Equal(ContractStatus.PendingApproval, contract.Status);

        var approval = await WithDbAsync(db => db.ApprovalInstances.AsNoTracking().SingleAsync(a => a.ContractId == contractId));
        Assert.Equal(approvalId, approval.Id);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
        Assert.Equal("sales01", approval.SubmittedBy);

        var history = await WithDbAsync(db => db.ContractHistories.AsNoTracking().SingleAsync(h => h.ContractId == contractId));
        Assert.Equal("Submit", history.Action);
        Assert.Equal(ContractStatus.Draft, history.FromStatus);
        Assert.Equal(ContractStatus.PendingApproval, history.ToStatus);

        // Đọc lại qua API
        var get = await Client.GetAsync($"/api/contracts/{contractId}");
        Assert.Equal("PendingApproval", (await BodyAsync(get)).GetProperty("status").GetString());
    }

    // TC08: submit lặp -> không tạo thêm kết quả xử lý trùng
    [Fact]
    public async Task TC08_SubmitTwiceWithSameKey_ReturnsSameApproval_AndCreatesOnlyOne()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var first = await SubmitAsync(contractId, key: "tc08");
        var second = await SubmitAsync(contractId, key: "tc08");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstId = (await BodyAsync(first)).GetProperty("approvalId").GetGuid();
        var secondId = (await BodyAsync(second)).GetProperty("approvalId").GetGuid();
        Assert.Equal(firstId, secondId);

        Assert.Equal(1, await WithDbAsync(db => db.ApprovalInstances.CountAsync(a => a.ContractId == contractId)));
        Assert.Equal(1, await WithDbAsync(db => db.ContractHistories.CountAsync(h => h.ContractId == contractId)));
    }

    [Fact]
    public async Task TC08_SubmitTwiceWithDifferentKey_ReturnsConflict_AndCreatesOnlyOne()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var first = await SubmitAsync(contractId, key: "key-A");
        var second = await SubmitAsync(contractId, key: "key-B");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("INVALID_STATE", (await BodyAsync(second)).GetProperty("code").GetString());
        Assert.Equal(1, await WithDbAsync(db => db.ApprovalInstances.CountAsync(a => a.ContractId == contractId)));
    }

    // TC09 (slide TC06): nhiều yêu cầu đồng thời -> chỉ một phiên phê duyệt
    [Fact]
    public async Task TC09_ConcurrentSubmits_CreateExactlyOneApproval()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(i => SubmitAsync(contractId, key: $"concurrent-{i}")));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.All(statuses.Where(s => s != HttpStatusCode.OK),
            s => Assert.Equal(HttpStatusCode.Conflict, s));

        Assert.Equal(1, await WithDbAsync(db => db.ApprovalInstances.CountAsync(a => a.ContractId == contractId)));
        var contract = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contractId));
        Assert.Equal(ContractStatus.PendingApproval, contract.Status);
    }

    // AC01: không có quyền -> 403, không đổi hợp đồng
    [Fact]
    public async Task AC01_SubmitWithoutPermission_ReturnsForbidden_AndContractUnchanged()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var res = await SubmitAsync(contractId, key: "ac01", role: "Accountant", userId: "kt01");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await BodyAsync(res)).GetProperty("code").GetString());
        var saved = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contractId));
        Assert.Equal(ContractStatus.Draft, saved.Status);
        Assert.Equal(0, await WithDbAsync(db => db.ApprovalInstances.CountAsync(a => a.ContractId == contractId)));
    }

    // Thiếu header vai trò cũng bị từ chối
    [Fact]
    public async Task SubmitWithoutRoleHeader_ReturnsForbidden()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var res = await SubmitAsync(contractId, key: "no-role", role: "");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // Hợp đồng không tồn tại -> 404
    [Fact]
    public async Task SubmitUnknownContract_ReturnsNotFound()
    {
        var res = await SubmitAsync(Guid.NewGuid(), key: "unknown");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("CONTRACT_NOT_FOUND", (await BodyAsync(res)).GetProperty("code").GetString());
    }

    // CTR-02: khách hàng không còn hoạt động
    [Fact]
    public async Task SubmitContractOfInactiveCustomer_ReturnsBadRequest()
    {
        var name = $"KH-Inactive-{Guid.NewGuid():N}";
        var customerId = await CreateCustomerAsync(name);
        var contractId = await CreateDraftContractAsync(customerId, complete: true);

        var deactivate = await Client.PutAsJsonAsync($"/api/customers/{customerId}",
            new { name, status = "Inactive" });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var res = await SubmitAsync(contractId, key: "inactive");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("CTR-02.CUSTOMER_INVALID", ErrorCodes(await BodyAsync(res)));
        var saved = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contractId));
        Assert.Equal(ContractStatus.Draft, saved.Status);
    }

    // Quy trình đầy đủ như video demo: thiếu dữ liệu -> bổ sung -> gửi lại thành công
    [Fact]
    public async Task FullFlow_FailThenFixThenSubmit_Succeeds()
    {
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateDraftContractAsync(customerId, complete: false);

        var failed = await SubmitAsync(contractId, key: "flow");
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);

        var fixedRes = await UpdateContractAsync(contractId, customerId, "Dịch vụ cảng", "Đã bổ sung nội dung");
        Assert.Equal(HttpStatusCode.OK, fixedRes.StatusCode);

        var ok = await SubmitAsync(contractId, key: "flow");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("PendingApproval", (await BodyAsync(ok)).GetProperty("status").GetString());
    }
}
