namespace QTKD.Api.Domain;

public static class ContractRules
{
    private static readonly Dictionary<ContractStatus, ContractStatus[]> Transitions = new()
    {
        [ContractStatus.Draft] = [ContractStatus.PendingApproval],
        [ContractStatus.PendingApproval] = [ContractStatus.UnderReview, ContractStatus.RevisionRequested,
                                            ContractStatus.Approved, ContractStatus.Rejected, ContractStatus.Cancelled],
        [ContractStatus.Submitted] = [ContractStatus.UnderReview, ContractStatus.Cancelled],
        [ContractStatus.UnderReview] = [ContractStatus.Approved, ContractStatus.Rejected, ContractStatus.RevisionRequested],
        [ContractStatus.RevisionRequested] = [ContractStatus.Draft],
        [ContractStatus.Approved] = [ContractStatus.Active],
        [ContractStatus.Active] = [ContractStatus.Expired],
        [ContractStatus.Rejected] = [],
        [ContractStatus.Cancelled] = [],
        [ContractStatus.Expired] = []
    };

    /// CTR-01: chỉ sửa khi Draft hoặc RevisionRequested.
    public static bool IsEditable(ContractStatus s) =>
        s is ContractStatus.Draft or ContractStatus.RevisionRequested;

    /// CTR-03: Draft chỉ được sang PendingApproval, không sang Approved trực tiếp.
    public static bool CanTransition(ContractStatus from, ContractStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
}