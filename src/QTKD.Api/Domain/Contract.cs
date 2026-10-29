namespace QTKD.Api.Domain;

public class Contract
{
    public Guid Id { get; set; }
    public string ContractNo { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? ServiceGroup { get; set; }
    public string? Content { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
}