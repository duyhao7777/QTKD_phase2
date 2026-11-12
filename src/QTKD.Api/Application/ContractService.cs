using Microsoft.EntityFrameworkCore;
using Npgsql;
using QTKD.Api.Domain;
using QTKD.Api.Infrastructure;

namespace QTKD.Api.Application;

public class ContractService(QtkdDbContext db)
{
    private static IQueryable<ContractResponse> Project(IQueryable<Contract> q) =>
        q.Select(x => new ContractResponse(x.Id, x.ContractNo, x.CustomerId, x.Customer.Name,
            x.StartDate, x.EndDate, x.ServiceGroup, x.Content, x.Status));

    public async Task<List<ContractResponse>> GetAllAsync(CancellationToken ct) =>
        await Project(db.Contracts.AsNoTracking().OrderBy(x => x.ContractNo)).ToListAsync(ct);

    public async Task<ContractResponse> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.Contracts.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng");

    public async Task<ContractResponse> CreateAsync(ContractCreateRequest r, CancellationToken ct)
    {
        var errors = new List<ErrorItem>();
        if (string.IsNullOrWhiteSpace(r.ContractNo))
            errors.Add(new("CONTRACT_NO_REQUIRED", "Số hợp đồng là bắt buộc"));
        else if (r.ContractNo.Trim().Length > 50)
            errors.Add(new("CONTRACT_NO_TOO_LONG", "Số hợp đồng tối đa 50 ký tự"));
        ValidateCommon(r.CustomerId, r.StartDate, r.EndDate, errors);
        ValidationFailedException.ThrowIfAny(errors);

        await EnsureCustomerExistsAsync(r.CustomerId, ct);

        var no = r.ContractNo!.Trim();
        if (await db.Contracts.AnyAsync(x => x.ContractNo == no, ct))
            throw new ConflictException("CONTRACT_NO_DUPLICATE", $"Số hợp đồng {no} đã tồn tại");

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNo = no,
            CustomerId = r.CustomerId,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            ServiceGroup = r.ServiceGroup,
            Content = r.Content,
            Status = ContractStatus.Draft
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync(ct);
        return await GetAsync(contract.Id, ct);
    }

    public async Task<ContractResponse> UpdateAsync(Guid id, ContractUpdateRequest r, CancellationToken ct)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct)
                       ?? throw new NotFoundException("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng");

        // CTR-01: chỉ sửa khi Draft hoặc RevisionRequested
        if (!ContractRules.IsEditable(contract.Status))
            throw new ConflictException("CTR-01",
                "Chỉ được chỉnh sửa hợp đồng ở trạng thái Draft hoặc RevisionRequested");

        var errors = new List<ErrorItem>();
        ValidateCommon(r.CustomerId, r.StartDate, r.EndDate, errors);
        ValidationFailedException.ThrowIfAny(errors);
        await EnsureCustomerExistsAsync(r.CustomerId, ct);

        contract.CustomerId = r.CustomerId;
        contract.StartDate = r.StartDate;
        contract.EndDate = r.EndDate;
        contract.ServiceGroup = r.ServiceGroup;
        contract.Content = r.Content;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// US03: gửi hợp đồng trình duyệt.
    public async Task<SubmitResponse> SubmitAsync(Guid id, UserContext user, string? idempotencyKey,
        string? comment, CancellationToken ct)
    {
        // 1. Kiểm tra quyền (AC01)
        if (!user.CanSubmitContract)
            throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền gửi hợp đồng trình duyệt");

        // 2. Tìm hợp đồng
        var contract = await db.Contracts.Include(c => c.Customer).FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng");

        // 3. Kiểm tra trạng thái (AC02) và gửi lặp (AC05)
        if (contract.Status == ContractStatus.PendingApproval)
        {
            var replay = await FindApprovalByKeyAsync(id, idempotencyKey, ct);
            if (replay is not null)
                return new SubmitResponse(id, ContractStatus.PendingApproval.ToString(), replay.Id);
            throw new ConflictException("INVALID_STATE", "Hợp đồng đã được gửi trình duyệt");
        }
        if (contract.Status != ContractStatus.Draft)
            throw new ConflictException("INVALID_STATE",
                $"Không thể gửi hợp đồng ở trạng thái {contract.Status}");

        // CTR-03: chỉ cho phép các chuyển trạng thái hợp lệ (Draft chỉ sang PendingApproval)
        if (!ContractRules.CanTransition(contract.Status, ContractStatus.PendingApproval))
            throw new ConflictException("CTR-03", "Chuyển trạng thái không hợp lệ");

        // 4. Kiểm tra CTR-02 (AC03)
        var errors = ValidateForSubmit(contract);
        if (errors.Count > 0)
            throw new ValidationFailedException("CTR-02", "Hợp đồng chưa đủ điều kiện gửi trình duyệt", errors);

        // 5-6. Tạo phiên phê duyệt, đổi trạng thái, ghi lịch sử (AC04)
        //      Một lần SaveChanges = một transaction: lỗi thì không đổi trạng thái.
        var now = DateTime.UtcNow;
        var from = contract.Status;
        var approval = new ApprovalInstance
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Status = ApprovalStatus.Pending,
            SubmittedBy = user.UserId,
            SubmittedAtUtc = now,
            Comment = comment,
            IdempotencyKey = idempotencyKey
        };
        contract.Status = ContractStatus.PendingApproval;
        db.ApprovalInstances.Add(approval);
        db.ContractHistories.Add(new ContractHistory
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Action = "Submit",
            FromStatus = from,
            ToStatus = ContractStatus.PendingApproval,
            ActedBy = user.UserId,
            ActedAtUtc = now,
            Comment = comment
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DeadlockDetected })
        {
            // Có yêu cầu khác đã gửi cùng hợp đồng trước (AC05)
            db.ChangeTracker.Clear();
            var replay = await FindApprovalByKeyAsync(id, idempotencyKey, ct);
            if (replay is not null)
                return new SubmitResponse(id, ContractStatus.PendingApproval.ToString(), replay.Id);
            throw new ConflictException("CONCURRENT_SUBMIT", "Hợp đồng đang được gửi bởi yêu cầu khác");
        }

        return new SubmitResponse(contract.Id, contract.Status.ToString(), approval.Id);
    }

    private Task<ApprovalInstance?> FindApprovalByKeyAsync(Guid contractId, string? key, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(key)
            ? Task.FromResult<ApprovalInstance?>(null)
            : db.ApprovalInstances.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ContractId == contractId && a.IdempotencyKey == key, ct);

    /// CTR-02 (giả định, xem docs/api.md mục 4)
    public static List<ErrorItem> ValidateForSubmit(Contract c)
    {
        var e = new List<ErrorItem>();
        if (c.Customer.Status != CustomerStatus.Active)
            e.Add(new("CTR-02.CUSTOMER_INVALID", "Khách hàng không hợp lệ hoặc không còn hoạt động"));
        if (c.StartDate == default || c.EndDate == default || c.StartDate > c.EndDate)
            e.Add(new("CTR-02.PERIOD_INVALID", "Thời gian hiệu lực không hợp lệ"));
        if (string.IsNullOrWhiteSpace(c.Content))
            e.Add(new("CTR-02.CONTENT_MISSING", "Thiếu nội dung hợp đồng"));
        if (string.IsNullOrWhiteSpace(c.ServiceGroup))
            e.Add(new("CTR-02.SERVICE_GROUP_MISSING", "Thiếu nhóm dịch vụ"));
        return e;
    }

    private static void ValidateCommon(Guid customerId, DateOnly start, DateOnly end, List<ErrorItem> errors)
    {
        if (customerId == Guid.Empty)
            errors.Add(new("CUSTOMER_REQUIRED", "Khách hàng là bắt buộc"));
        if (start == default)
            errors.Add(new("START_DATE_REQUIRED", "Ngày bắt đầu hiệu lực là bắt buộc"));
        if (end == default)
            errors.Add(new("END_DATE_REQUIRED", "Ngày kết thúc hiệu lực là bắt buộc"));
        if (start != default && end != default && start > end)
            errors.Add(new("PERIOD_INVALID", "Ngày bắt đầu không được lớn hơn ngày kết thúc"));
    }

    private async Task EnsureCustomerExistsAsync(Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            throw new ValidationFailedException("CUSTOMER_NOT_FOUND", "Khách hàng không tồn tại");
    }
}