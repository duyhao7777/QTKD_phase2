namespace QTKD.Api.Domain;

public class ApprovalInstance
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = default!;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string SubmittedBy { get; set; } = default!;
    public DateTime SubmittedAtUtc { get; set; }
    public string? Comment { get; set; }
    public string? IdempotencyKey { get; set; }
}