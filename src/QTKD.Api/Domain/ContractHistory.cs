namespace QTKD.Api.Domain;

public class ContractHistory
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = default!;
    public string Action { get; set; } = default!;
    public ContractStatus? FromStatus { get; set; }
    public ContractStatus ToStatus { get; set; }
    public string ActedBy { get; set; } = default!;
    public DateTime ActedAtUtc { get; set; }
    public string? Comment { get; set; }
}