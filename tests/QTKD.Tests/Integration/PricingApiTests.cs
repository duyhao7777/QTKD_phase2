using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Domain;
using Xunit;

namespace QTKD.Tests.Integration;

public class PricingApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static object ValidPriceList(string? name = null) => new
    {
        name = name ?? $"Bảng giá {Guid.NewGuid():N}",
        serviceGroup = "Dịch vụ cảng",
        effectiveFrom = new DateOnly(2027, 1, 1),
        effectiveTo = new DateOnly(2027, 12, 31)
    };

    [Fact]
    public async Task CreatePriceList_ThenAddAndReadItems_Works()
    {
        var create = await Client.PostAsJsonAsync("/api/pricelists", ValidPriceList());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await BodyAsync(create);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());

        var item1 = await Client.PostAsJsonAsync($"/api/pricelists/{id}/items",
            new { serviceCode = "CANG-201", unitPrice = 150000 });
        var item2 = await Client.PostAsJsonAsync($"/api/pricelists/{id}/items",
            new { serviceCode = "CANG-202", unitPrice = 220000 });
        Assert.Equal(HttpStatusCode.OK, item1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, item2.StatusCode);

        var get = await Client.GetAsync($"/api/pricelists/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await BodyAsync(get);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());

        var items = await WithDbAsync(db => db.PriceItems.AsNoTracking().Where(i => i.PriceListId == id).ToListAsync());
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.ServiceCode == "CANG-201" && i.UnitPrice == 150000m);
    }

    // PRC-02: ngày bắt đầu không được lớn hơn ngày kết thúc
    [Fact]
    public async Task PRC02_StartAfterEnd_ReturnsBadRequest()
    {
        var res = await Client.PostAsJsonAsync("/api/pricelists", new
        {
            name = $"Bảng giá sai ngày {Guid.NewGuid():N}",
            serviceGroup = "Dịch vụ cảng",
            effectiveFrom = new DateOnly(2027, 12, 31),
            effectiveTo = new DateOnly(2027, 1, 1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("PRC-02", ErrorCodes(await BodyAsync(res)));
    }

    // PRC-01: phải gắn khách hàng hoặc nhóm dịch vụ
    [Fact]
    public async Task PRC01_WithoutScope_ReturnsBadRequest()
    {
        var res = await Client.PostAsJsonAsync("/api/pricelists", new
        {
            name = $"Bảng giá không phạm vi {Guid.NewGuid():N}",
            effectiveFrom = new DateOnly(2027, 1, 1),
            effectiveTo = new DateOnly(2027, 12, 31)
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("PRC-01", ErrorCodes(await BodyAsync(res)));
    }

    [Fact]
    public async Task AddPriceItem_WithInvalidPrice_ReturnsBadRequest()
    {
        var create = await Client.PostAsJsonAsync("/api/pricelists", ValidPriceList());
        var id = (await BodyAsync(create)).GetProperty("id").GetGuid();

        var res = await Client.PostAsJsonAsync($"/api/pricelists/{id}/items",
            new { serviceCode = "CANG-301", unitPrice = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("UNIT_PRICE_INVALID", ErrorCodes(await BodyAsync(res)));
    }

    [Fact]
    public async Task AddPriceItem_WithDuplicateServiceCode_ReturnsConflict()
    {
        var create = await Client.PostAsJsonAsync("/api/pricelists", ValidPriceList());
        var id = (await BodyAsync(create)).GetProperty("id").GetGuid();
        await Client.PostAsJsonAsync($"/api/pricelists/{id}/items", new { serviceCode = "CANG-401", unitPrice = 100000 });

        var res = await Client.PostAsJsonAsync($"/api/pricelists/{id}/items",
            new { serviceCode = "CANG-401", unitPrice = 120000 });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("SERVICE_CODE_DUPLICATE", (await BodyAsync(res)).GetProperty("code").GetString());
        Assert.Equal(1, await WithDbAsync(db => db.PriceItems.CountAsync(i => i.PriceListId == id)));
    }

    [Fact]
    public async Task GetUnknownPriceList_ReturnsNotFound()
    {
        var res = await Client.GetAsync($"/api/pricelists/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
