using QTKD.Api.Domain;
using Xunit;

namespace QTKD.Tests.Unit;

/// <summary>Unit test thuần logic, không cần database.</summary>
public class ContractRulesTests
{
    // ===== CTR-01: chỉ được sửa hợp đồng ở Draft hoặc RevisionRequested =====

    [Theory]
    [InlineData(ContractStatus.Draft)]
    [InlineData(ContractStatus.RevisionRequested)]
    public void CTR01_Editable_WhenDraftOrRevisionRequested(ContractStatus status)
    {
        Assert.True(ContractRules.IsEditable(status));
    }

    [Theory]
    [InlineData(ContractStatus.Submitted)]
    [InlineData(ContractStatus.PendingApproval)]
    [InlineData(ContractStatus.UnderReview)]
    [InlineData(ContractStatus.Approved)]
    [InlineData(ContractStatus.Active)]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Expired)]
    public void CTR01_NotEditable_InAnyOtherStatus(ContractStatus status)
    {
        Assert.False(ContractRules.IsEditable(status));
    }

    // ===== CTR-03: không được chuyển Draft thẳng sang Approved =====

    [Fact]
    public void TC07_CTR03_DraftToApproved_IsRejected()
    {
        Assert.False(ContractRules.CanTransition(ContractStatus.Draft, ContractStatus.Approved));
    }

    [Fact]
    public void CTR03_DraftToPendingApproval_IsAllowed()
    {
        Assert.True(ContractRules.CanTransition(ContractStatus.Draft, ContractStatus.PendingApproval));
    }

    [Theory]
    [InlineData(ContractStatus.Submitted)]
    [InlineData(ContractStatus.UnderReview)]
    [InlineData(ContractStatus.RevisionRequested)]
    [InlineData(ContractStatus.Approved)]
    [InlineData(ContractStatus.Active)]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Expired)]
    public void CTR03_Draft_CannotJumpToAnyStatusExceptPendingApproval(ContractStatus target)
    {
        Assert.False(ContractRules.CanTransition(ContractStatus.Draft, target));
    }

    [Theory]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Expired)]
    public void CTR03_FinalStatuses_HaveNoOutgoingTransition(ContractStatus from)
    {
        foreach (var to in Enum.GetValues<ContractStatus>())
            Assert.False(ContractRules.CanTransition(from, to), $"{from} -> {to} không được phép");
    }
}
