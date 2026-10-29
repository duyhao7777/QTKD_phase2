namespace QTKD.Api.Domain;

public class PriceList
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public Guid? CustomerId { get; set; }
    public string? ServiceGroup { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo { get; set; }
    public PriceListStatus Status { get; set; } = PriceListStatus.Draft;
    public List<PriceItem> Items { get; set; } = new();
}

public class PriceItem
{
    public Guid PriceListId { get; set; }
    public PriceList PriceList { get; set; } = default!;
    public string ServiceCode { get; set; } = default!;
    public decimal UnitPrice { get; set; }
}