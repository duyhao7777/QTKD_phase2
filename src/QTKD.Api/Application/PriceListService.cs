using Microsoft.EntityFrameworkCore;
using QTKD.Api.Domain;
using QTKD.Api.Infrastructure;

namespace QTKD.Api.Application;

public class PriceListService(QtkdDbContext db)
{
    private static IQueryable<PriceListResponse> Project(IQueryable<PriceList> q) =>
        q.Select(p => new PriceListResponse(p.Id, p.Name, p.CustomerId, p.ServiceGroup,
            p.EffectiveFrom, p.EffectiveTo, p.Status,
            p.Items.OrderBy(i => i.ServiceCode)
                   .Select(i => new PriceItemResponse(i.ServiceCode, i.UnitPrice)).ToList()));

    public async Task<List<PriceListResponse>> GetAllAsync(CancellationToken ct) =>
        await Project(db.PriceLists.AsNoTracking().OrderBy(p => p.Name)).ToListAsync(ct);

    public async Task<PriceListResponse> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.PriceLists.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("PRICELIST_NOT_FOUND", "Không tìm thấy bảng giá");

    public async Task<List<PriceItemResponse>> GetItemsAsync(Guid id, CancellationToken ct)
    {
        if (!await db.PriceLists.AnyAsync(p => p.Id == id, ct))
            throw new NotFoundException("PRICELIST_NOT_FOUND", "Không tìm thấy bảng giá");
        return await db.PriceItems.AsNoTracking().Where(i => i.PriceListId == id)
            .OrderBy(i => i.ServiceCode)
            .Select(i => new PriceItemResponse(i.ServiceCode, i.UnitPrice)).ToListAsync(ct);
    }

    public async Task<PriceListResponse> CreateAsync(PriceListCreateRequest r, CancellationToken ct)
    {
        var errors = new List<ErrorItem>();
        if (string.IsNullOrWhiteSpace(r.Name))
            errors.Add(new("PRICELIST_NAME_REQUIRED", "Tên bảng giá là bắt buộc"));
        // PRC-01: phải gắn khách hàng hoặc nhóm dịch vụ
        if (r.CustomerId is null && string.IsNullOrWhiteSpace(r.ServiceGroup))
            errors.Add(new("PRC-01", "Bảng giá phải gắn với khách hàng hoặc nhóm dịch vụ"));
        // PRC-02: có thời gian hiệu lực, bắt đầu không lớn hơn kết thúc
        if (r.EffectiveFrom == default || r.EffectiveTo == default)
            errors.Add(new("PRC-02.REQUIRED", "Bảng giá phải có thời gian hiệu lực"));
        else if (r.EffectiveFrom > r.EffectiveTo)
            errors.Add(new("PRC-02", "Ngày bắt đầu không được lớn hơn ngày kết thúc"));
        ValidationFailedException.ThrowIfAny(errors);

        if (r.CustomerId is not null && !await db.Customers.AnyAsync(c => c.Id == r.CustomerId, ct))
            throw new ValidationFailedException("CUSTOMER_NOT_FOUND", "Khách hàng không tồn tại");

        var p = new PriceList
        {
            Id = Guid.NewGuid(),
            Name = r.Name!.Trim(),
            CustomerId = r.CustomerId,
            ServiceGroup = r.ServiceGroup,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            Status = PriceListStatus.Draft
        };
        db.PriceLists.Add(p);
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, ct);
    }

    public async Task<PriceItemResponse> AddItemAsync(Guid id, PriceItemRequest r, CancellationToken ct)
    {
        var list = await db.PriceLists.FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw new NotFoundException("PRICELIST_NOT_FOUND", "Không tìm thấy bảng giá");
        if (list.Status != PriceListStatus.Draft)
            throw new ConflictException("PRICELIST_NOT_EDITABLE", "Chỉ thêm đơn giá khi bảng giá ở trạng thái Draft");

        var errors = new List<ErrorItem>();
        if (string.IsNullOrWhiteSpace(r.ServiceCode))
            errors.Add(new("SERVICE_CODE_REQUIRED", "Mã dịch vụ là bắt buộc"));
        else if (r.ServiceCode.Trim().Length > 50)
            errors.Add(new("SERVICE_CODE_TOO_LONG", "Mã dịch vụ tối đa 50 ký tự"));
        if (r.UnitPrice <= 0)
            errors.Add(new("UNIT_PRICE_INVALID", "Đơn giá phải lớn hơn 0"));
        ValidationFailedException.ThrowIfAny(errors);

        var code = r.ServiceCode!.Trim();
        if (await db.PriceItems.AnyAsync(i => i.PriceListId == id && i.ServiceCode == code, ct))
            throw new ConflictException("SERVICE_CODE_DUPLICATE", $"Mã dịch vụ {code} đã có trong bảng giá");

        db.PriceItems.Add(new PriceItem { PriceListId = id, ServiceCode = code, UnitPrice = r.UnitPrice });
        await db.SaveChangesAsync(ct);
        return new PriceItemResponse(code, r.UnitPrice);
    }
}