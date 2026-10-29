namespace QTKD.Api.Domain;

public enum CustomerStatus { Active, Inactive }

public enum ContractStatus
{
    Draft, Submitted, PendingApproval, UnderReview, RevisionRequested,
    Approved, Active, Rejected, Cancelled, Expired
}

public enum PriceListStatus
{
    Draft, Submitted, Approved, Rejected, Effective, Superseded, Expired
}