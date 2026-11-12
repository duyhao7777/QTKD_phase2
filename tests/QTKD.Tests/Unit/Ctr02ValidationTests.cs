using QTKD.Api.Application;
using QTKD.Api.Domain;
using Xunit;

namespace QTKD.Tests.Unit;

/// <summary>
/// CTR-02: chỉ gửi khi khách hàng hợp lệ, thời gian hiệu lực hợp lệ, có nội dung bắt buộc.
/// Mỗi điều kiện có một trường hợp đúng và một trường hợp sai.
/// </summary>
public class Ctr02ValidationTests
{
    private static Contract ValidContract()
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Công ty Minh Phát",
            Status = CustomerStatus.Active
        };
        return new Contract
        {
            Id = Guid.NewGuid(),
            ContractNo = "HD-UNIT-001",
            CustomerId = customer.Id,
            Customer = customer,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2027, 9, 30),
            ServiceGroup = "Dịch vụ cảng",
            Content = "Nội dung hợp đồng",
            Status = ContractStatus.Draft
        };
    }

    private static List<string> Codes(Contract c) =>
        ContractService.ValidateForSubmit(c).Select(e => e.Code).ToList();

    [Fact]
    public void CTR02_ValidContract_HasNoErrors()
    {
        Assert.Empty(ContractService.ValidateForSubmit(ValidContract()));
    }

    // ----- Khách hàng -----

    [Fact]
    public void CTR02_InactiveCustomer_IsRejected()
    {
        var c = ValidContract();
        c.Customer.Status = CustomerStatus.Inactive;

        Assert.Contains("CTR-02.CUSTOMER_INVALID", Codes(c));
    }

    // ----- Thời gian hiệu lực -----

    [Fact]
    public void CTR02_StartDateAfterEndDate_IsRejected()
    {
        var c = ValidContract();
        c.StartDate = new DateOnly(2027, 10, 1);
        c.EndDate = new DateOnly(2026, 9, 30);

        Assert.Contains("CTR-02.PERIOD_INVALID", Codes(c));
    }

    [Fact]
    public void CTR02_StartDateEqualsEndDate_IsAccepted()
    {
        var c = ValidContract();
        c.StartDate = new DateOnly(2026, 10, 1);
        c.EndDate = new DateOnly(2026, 10, 1);

        Assert.DoesNotContain("CTR-02.PERIOD_INVALID", Codes(c));
    }

    // ----- Nội dung bắt buộc -----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CTR02_MissingContent_IsRejected(string? content)
    {
        var c = ValidContract();
        c.Content = content;

        Assert.Contains("CTR-02.CONTENT_MISSING", Codes(c));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CTR02_MissingServiceGroup_IsRejected(string? serviceGroup)
    {
        var c = ValidContract();
        c.ServiceGroup = serviceGroup;

        Assert.Contains("CTR-02.SERVICE_GROUP_MISSING", Codes(c));
    }

    [Fact]
    public void CTR02_MissingEverything_ReportsAllUnmetConditions()
    {
        var c = ValidContract();
        c.Customer.Status = CustomerStatus.Inactive;
        c.Content = null;
        c.ServiceGroup = null;

        var codes = Codes(c);

        Assert.Contains("CTR-02.CUSTOMER_INVALID", codes);
        Assert.Contains("CTR-02.CONTENT_MISSING", codes);
        Assert.Contains("CTR-02.SERVICE_GROUP_MISSING", codes);
        Assert.Equal(3, codes.Count);
    }
}
