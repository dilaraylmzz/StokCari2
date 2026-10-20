using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StokCari.Core.Enums;
using StokCari.Core.Entities;
using StokCari.Core.Interfaces;

namespace StokCari.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ReceiptSeries> ReceiptSeries => Set<ReceiptSeries>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<StockTrans> StockTrans => Set<StockTrans>();
    public DbSet<ActTrans> ActTrans => Set<ActTrans>();

    /// <summary>Global query filter tarafından kullanılır (istek başına değerlendirilir).</summary>
    public Guid? CurrentCompanyId => currentUser.CompanyId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Kolon adları DBML şemasındaki gibi snake_case.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                // RowVersion, PostgreSQL'de xmin sistem kolonuna eşlenir; adı değiştirilmemelidir.
                if (property.Name == nameof(Stock.RowVersion)) continue;

                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }

        // Soft delete + çok kiracılı (multi-tenant) izolasyon.
        // CurrentCompanyId == null: Admin (başlık yok) veya kimliksiz istek (login) -> şirket filtresi yok.
        modelBuilder.Entity<Company>().HasQueryFilter(e =>
            !e.IsDeleted && (CurrentCompanyId == null || e.Id == CurrentCompanyId));

        modelBuilder.Entity<User>().HasQueryFilter(e =>
            !e.IsDeleted && (CurrentCompanyId == null || e.CompanyId == CurrentCompanyId));

        modelBuilder.Entity<Stock>().HasQueryFilter(e =>
            !e.IsDeleted && (CurrentCompanyId == null || e.CompanyId == CurrentCompanyId));

        modelBuilder.Entity<Account>().HasQueryFilter(e =>
            !e.IsDeleted && (CurrentCompanyId == null || e.CompanyId == CurrentCompanyId));

        modelBuilder.Entity<Receipt>().HasQueryFilter(e =>
            !e.IsDeleted && (CurrentCompanyId == null || e.CompanyId == CurrentCompanyId));

        modelBuilder.Entity<StockTrans>().HasQueryFilter(e =>
            CurrentCompanyId == null || e.CompanyId == CurrentCompanyId);

        modelBuilder.Entity<ActTrans>().HasQueryFilter(e =>
            CurrentCompanyId == null || e.CompanyId == CurrentCompanyId);

        modelBuilder.Entity<ReceiptSeries>().HasQueryFilter(e =>
            CurrentCompanyId == null || e.CompanyId == CurrentCompanyId);
    }

    /// <summary>
    /// Geçerli stok hareketleri: fişsiz hareketler ile bağlı fişi Approved olanlar.
    /// Hareketlerden toplam alan HER sorgu bunu kullanmalıdır (iptalli fişin hareketi sayılmaz).
    /// </summary>
    public IQueryable<StockTrans> EffectiveStockTrans() =>
        StockTrans.Where(t => t.ReceiptId == null || Receipts.Any(r => r.Id == t.ReceiptId && r.Status == ReceiptStatus.Approved));

    /// <summary>Geçerli cari hareketleri: fişsiz hareketler ile bağlı fişi Approved olanlar.</summary>
    public IQueryable<ActTrans> EffectiveActTrans() =>
        ActTrans.Where(t => t.ReceiptId == null || Receipts.Any(r => r.Id == t.ReceiptId && r.Status == ReceiptStatus.Approved));

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
