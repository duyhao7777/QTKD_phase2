using QTKD.Api.Domain;

namespace QTKD.Api.Application;

// Customer
public record CustomerRequest(string? Name, CustomerStatus? Status);
public record CustomerResponse(Guid Id, string Name, CustomerStatus Status);

// Contract
public record ContractCreateRequest(string? ContractNo, Guid CustomerId, DateOnly StartDate,
    DateOnly EndDate, string? ServiceGroup, string? Content);
public record ContractUpdateRequest(Guid CustomerId, DateOnly StartDate, DateOnly EndDate,
    string? ServiceGroup, string? Content);
public record ContractResponse(Guid Id, string ContractNo, Guid CustomerId, string CustomerName,
    DateOnly StartDate, DateOnly EndDate, string? ServiceGroup, string? Content, ContractStatus Status);
public record SubmitRequest(string? Comment);
public record SubmitResponse(Guid ContractId, string Status, Guid ApprovalId);

// Pricing
public record PriceListCreateRequest(string? Name, Guid? CustomerId, string? ServiceGroup,
    DateOnly EffectiveFrom, DateOnly EffectiveTo);
public record PriceItemRequest(string? ServiceCode, decimal UnitPrice);
public record PriceItemResponse(string ServiceCode, decimal UnitPrice);
public record PriceListResponse(Guid Id, string Name, Guid? CustomerId, string? ServiceGroup,
    DateOnly EffectiveFrom, DateOnly EffectiveTo, PriceListStatus Status, List<PriceItemResponse> Items);