using Microsoft.EntityFrameworkCore;
using QTKD.Api.Domain;

namespace QTKD.Api.Infrastructure;

public class QtkdDbContext(DbContextOptions<QtkdDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceItem> PriceItems => Set<PriceItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        });

        b.Entity<Contract>(e =>
        {
            e.HasIndex(x => x.ContractNo).IsUnique();
            e.Property(x => x.ContractNo).IsRequired().HasMaxLength(50);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Customer).WithMany(c => c.Contracts)
             .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PriceList>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        });

        b.Entity<PriceItem>(e =>
        {
            e.HasKey(x => new { x.PriceListId, x.ServiceCode });
            e.Property(x => x.ServiceCode).HasMaxLength(50);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne(x => x.PriceList).WithMany(p => p.Items)
             .HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Cascade);
        });

        Seed(b);
    }

    private static void Seed(ModelBuilder b)
    {
        var c1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var c2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var ct = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var pl = Guid.Parse("44444444-4444-4444-4444-444444444444");

        b.Entity<Customer>().HasData(
            new Customer { Id = c1, Name = "Công ty Minh Phát", Status = CustomerStatus.Active },
            new Customer { Id = c2, Name = "Công ty Hải Long", Status = CustomerStatus.Active });

        b.Entity<Contract>().HasData(new Contract
        {
            Id = ct, ContractNo = "HD-2026-018", CustomerId = c1,
            StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2027, 9, 30),
            ServiceGroup = "Dịch vụ cảng", Status = ContractStatus.Draft
        });

        b.Entity<PriceList>().HasData(new PriceList
        {
            Id = pl, Name = "Bảng giá dịch vụ cảng 2026", CustomerId = c1,
            ServiceGroup = "Dịch vụ cảng",
            EffectiveFrom = new DateOnly(2026, 10, 1), EffectiveTo = new DateOnly(2027, 9, 30),
            Status = PriceListStatus.Draft
        });

        b.Entity<PriceItem>().HasData(
            new PriceItem { PriceListId = pl, ServiceCode = "CANG-001", UnitPrice = 150000m },
            new PriceItem { PriceListId = pl, ServiceCode = "CANG-002", UnitPrice = 220000m });
    }
}