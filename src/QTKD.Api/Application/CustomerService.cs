using Microsoft.EntityFrameworkCore;
using QTKD.Api.Domain;
using QTKD.Api.Infrastructure;

namespace QTKD.Api.Application;

public class CustomerService(QtkdDbContext db)
{
    public async Task<List<CustomerResponse>> GetAllAsync(CancellationToken ct) =>
        await db.Customers.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CustomerResponse(c.Id, c.Name, c.Status)).ToListAsync(ct);

    public async Task<CustomerResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.Customers.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new CustomerResponse(c.Id, c.Name, c.Status)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("CUSTOMER_NOT_FOUND", "Không tìm thấy khách hàng");

    public async Task<CustomerResponse> CreateAsync(CustomerRequest r, CancellationToken ct)
    {
        Validate(r);
        var name = r.Name!.Trim();

        if (await db.Customers.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("CUSTOMER_NAME_DUPLICATE", $"Khách hàng '{name}' đã tồn tại");

        var c = new Customer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = r.Status ?? CustomerStatus.Active
        };
        db.Customers.Add(c);
        await db.SaveChangesAsync(ct);
        return new CustomerResponse(c.Id, c.Name, c.Status);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest r, CancellationToken ct)
    {
        Validate(r);
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("CUSTOMER_NOT_FOUND", "Không tìm thấy khách hàng");

        var name = r.Name!.Trim();
        if (await db.Customers.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("CUSTOMER_NAME_DUPLICATE", $"Khách hàng '{name}' đã tồn tại");

        c.Name = name;
        if (r.Status is not null) c.Status = r.Status.Value;
        await db.SaveChangesAsync(ct);
        return new CustomerResponse(c.Id, c.Name, c.Status);
    }

    private static void Validate(CustomerRequest r)
    {
        var errors = new List<ErrorItem>();
        if (string.IsNullOrWhiteSpace(r.Name))
            errors.Add(new("CUSTOMER_NAME_REQUIRED", "Tên khách hàng là bắt buộc"));
        else if (r.Name.Trim().Length > 200)
            errors.Add(new("CUSTOMER_NAME_TOO_LONG", "Tên khách hàng tối đa 200 ký tự"));
        ValidationFailedException.ThrowIfAny(errors);
    }
}