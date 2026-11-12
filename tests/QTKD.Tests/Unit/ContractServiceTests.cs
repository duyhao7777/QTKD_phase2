using Microsoft.EntityFrameworkCore;
using QTKD.Api.Application;
using QTKD.Api.Domain;
using QTKD.Api.Infrastructure;
using Xunit;

namespace QTKD.Tests.Unit;

/// <summary>
/// Kiểm tra nghiệp vụ của ContractService trên database in-memory.
/// Lưu ý: in-memory không có ràng buộc duy nhất thật như PostgreSQL;
/// phần chống gửi đồng thời được kiểm tra ở integration test.
/// </summary>
public class ContractServiceTests
{
    private static readonly UserContext Sales = new("sales01", "Sales");
    private static readonly UserContext Accountant = new("kt01", "Accountant");

    private static async Task<Contract> AddContractAsync(
        QtkdDbContext db,
        ContractStatus status = ContractStatus.Draft,
        string? content = "Nội dung hợp đồng",
        string? serviceGroup = "Dịch vụ cảng")
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = $"KH {Guid.NewGuid():N}",
            Status = CustomerStatus.Active
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNo = $"HD-U-{Guid.NewGuid():N}",
            CustomerId = customer.Id,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2027, 9, 30),
            ServiceGroup = serviceGroup,
            Content = content,
            Status = status
        };
        db.Customers.Add(customer);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract;
    }

    // ===== CTR-01 =====

    [Fact]
    public async Task TC03_Unit_UpdateDraftContract_Succeeds()
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db);

        var result = await svc.UpdateAsync(c.Id,
            new ContractUpdateRequest(c.CustomerId, c.StartDate, c.EndDate, "Nhóm mới", "Nội dung mới"),
            CancellationToken.None);

        Assert.Equal("Nội dung mới", result.Content);
        Assert.Equal(ContractStatus.Draft, result.Status);
    }

    [Fact]
    public async Task TC04_Unit_UpdateContractInPendingApproval_IsRejected_AndDataUnchanged()
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db, ContractStatus.PendingApproval);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            svc.UpdateAsync(c.Id,
                new ContractUpdateRequest(c.CustomerId, c.StartDate, c.EndDate, "Nhóm khác", "Nội dung bị sửa"),
                CancellationToken.None));

        Assert.Equal("CTR-01", ex.Code);
        var saved = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal("Nội dung hợp đồng", saved.Content);
        Assert.Equal("Dịch vụ cảng", saved.ServiceGroup);
    }

    // ===== CTR-02 =====

    [Fact]
    public async Task TC05_Unit_SubmitMissingContent_IsRejected_AndStaysDraft()
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db, content: null);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            svc.SubmitAsync(c.Id, Sales, "key-1", null, CancellationToken.None));

        Assert.Equal("CTR-02", ex.Code);
        Assert.Contains(ex.Errors, e => e.Code == "CTR-02.CONTENT_MISSING");
        var saved = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(ContractStatus.Draft, saved.Status);
        Assert.Equal(0, await db.ApprovalInstances.CountAsync(a => a.ContractId == c.Id));
        Assert.Equal(0, await db.ContractHistories.CountAsync(h => h.ContractId == c.Id));
    }

    [Fact]
    public async Task TC06_Unit_SubmitValidContract_MovesToPendingApproval_WithApprovalAndHistory()
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db);

        var result = await svc.SubmitAsync(c.Id, Sales, "key-1", "Đề nghị duyệt", CancellationToken.None);

        Assert.Equal("PendingApproval", result.Status);
        var saved = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(ContractStatus.PendingApproval, saved.Status);

        var approval = await db.ApprovalInstances.AsNoTracking().SingleAsync(a => a.ContractId == c.Id);
        Assert.Equal(result.ApprovalId, approval.Id);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
        Assert.Equal("sales01", approval.SubmittedBy);

        var history = await db.ContractHistories.AsNoTracking().SingleAsync(h => h.ContractId == c.Id);
        Assert.Equal("Submit", history.Action);
        Assert.Equal(ContractStatus.Draft, history.FromStatus);
        Assert.Equal(ContractStatus.PendingApproval, history.ToStatus);
    }

    // ===== AC01 =====

    [Fact]
    public async Task AC01_Unit_SubmitWithoutPermission_IsForbidden_AndNothingChanges()
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.SubmitAsync(c.Id, Accountant, "key-1", null, CancellationToken.None));

        Assert.Equal("FORBIDDEN", ex.Code);
        var saved = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(ContractStatus.Draft, saved.Status);
        Assert.Equal(0, await db.ApprovalInstances.CountAsync());
    }

    // ===== AC02 / CTR-03 =====

    [Theory]
    [InlineData(ContractStatus.Approved)]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Active)]
    public async Task AC02_Unit_SubmitWhenNotDraft_IsRejected_AndStatusUnchanged(ContractStatus status)
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db, status);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            svc.SubmitAsync(c.Id, Sales, "key-1", null, CancellationToken.None));

        Assert.Equal("INVALID_STATE", ex.Code);
        var saved = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(status, saved.Status);
        Assert.Equal(0, await db.ApprovalInstances.CountAsync(a => a.ContractId == c.Id));
    }

    // ===== AC05: gửi lặp =====

    [Fact]
    public async Task TC08_Unit_SubmitTwiceWithSameKey_ReturnsSameApproval_AndCreatesOnlyOne()
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db);

        var first = await svc.SubmitAsync(c.Id, Sales, "same-key", null, CancellationToken.None);
        var second = await svc.SubmitAsync(c.Id, Sales, "same-key", null, CancellationToken.None);

        Assert.Equal(first.ApprovalId, second.ApprovalId);
        Assert.Equal(1, await db.ApprovalInstances.CountAsync(a => a.ContractId == c.Id));
        Assert.Equal(1, await db.ContractHistories.CountAsync(h => h.ContractId == c.Id));
    }

    [Fact]
    public async Task TC08_Unit_SubmitTwiceWithDifferentKey_IsConflict_AndCreatesOnlyOne()
    {
        using var db = TestDb.Create();
        var svc = new ContractService(db);
        var c = await AddContractAsync(db);

        await svc.SubmitAsync(c.Id, Sales, "key-A", null, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            svc.SubmitAsync(c.Id, Sales, "key-B", null, CancellationToken.None));

        Assert.Equal("INVALID_STATE", ex.Code);
        Assert.Equal(1, await db.ApprovalInstances.CountAsync(a => a.ContractId == c.Id));
    }
}
