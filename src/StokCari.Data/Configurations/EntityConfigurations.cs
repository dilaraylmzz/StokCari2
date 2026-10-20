using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StokCari.Core.Entities;

namespace StokCari.Data.Configurations;

// Not: Kolon adları AppDbContext.OnModelCreating içinde snake_case'e çevrilir.
// Unique indeksler soft delete ile çakışmaması için filtrelidir (silinmiş kaydın kodu yeniden kullanılabilir).
// Tüm yabancı anahtarlar Restrict: silme işlemi soft delete ile yapıldığı için cascade kullanılmaz.

internal static class ConfigurationHelpers
{
    public const string NotDeleted = "is_deleted = false";

    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> b, bool createdByRequired) where T : BaseEntity
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        if (createdByRequired)
        {
            b.Property(x => x.CreatedBy).IsRequired();
        }
    }
}

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("companies");
        b.ConfigureAudit(createdByRequired: false);

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TaxNumber).HasMaxLength(20);

        b.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationHelpers.NotDeleted).HasDatabaseName("ux_companies_code");
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t =>
            t.HasCheckConstraint("ck_users_role_company", "\"role\" = 'Admin' OR company_id IS NOT NULL"));
        b.ConfigureAudit(createdByRequired: false);

        b.Property(x => x.Username).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(255).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.HasIndex(x => x.Username).IsUnique().HasFilter(ConfigurationHelpers.NotDeleted).HasDatabaseName("ux_users_username");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_users_company");

        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> b)
    {
        b.ToTable("stocks");
        b.ConfigureAudit(createdByRequired: true);

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.Balance).HasPrecision(18, 3);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter(ConfigurationHelpers.NotDeleted).HasDatabaseName("ux_stocks_company_code");

        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts");
        b.ConfigureAudit(createdByRequired: true);

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.TaxNumber).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Balance).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter(ConfigurationHelpers.NotDeleted).HasDatabaseName("ux_accounts_company_code");

        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReceiptSeriesConfiguration : IEntityTypeConfiguration<ReceiptSeries>
{
    public void Configure(EntityTypeBuilder<ReceiptSeries> b)
    {
        b.ToTable("receipt_series");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Series).HasMaxLength(10).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.CompanyId, x.Series }).IsUnique().HasDatabaseName("ux_receipt_series_company_series");

        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> b)
    {
        b.ToTable("receipts", t =>
        {
            t.HasCheckConstraint("ck_receipts_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_receipts_unit_price", "unit_price > 0");
        });
        b.ConfigureAudit(createdByRequired: true);

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Series).HasMaxLength(10).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => new { x.CompanyId, x.Series, x.Number }).IsUnique().HasFilter(ConfigurationHelpers.NotDeleted).HasDatabaseName("ux_receipts_company_series_number");
        b.HasIndex(x => new { x.CompanyId, x.ReceiptDate }).HasDatabaseName("ix_receipts_company_date");
        b.HasIndex(x => new { x.CompanyId, x.AccountId }).HasDatabaseName("ix_receipts_company_account");
        b.HasIndex(x => x.StockId).HasDatabaseName("ix_receipts_stock");

        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Stock>().WithMany().HasForeignKey(x => x.StockId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransConfiguration : IEntityTypeConfiguration<StockTrans>
{
    public void Configure(EntityTypeBuilder<StockTrans> b)
    {
        b.ToTable("stock_trans", t => t.HasCheckConstraint("ck_stock_trans_quantity", "quantity > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.CreatedBy).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.HasIndex(x => new { x.CompanyId, x.StockId, x.TransDate }).HasDatabaseName("ix_stock_trans_company_stock_date");
        b.HasIndex(x => x.ReceiptId).HasDatabaseName("ix_stock_trans_receipt");

        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Stock>().WithMany().HasForeignKey(x => x.StockId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ActTransConfiguration : IEntityTypeConfiguration<ActTrans>
{
    public void Configure(EntityTypeBuilder<ActTrans> b)
    {
        b.ToTable("act_trans");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.CreatedBy).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.HasIndex(x => new { x.CompanyId, x.AccountId, x.TransDate }).HasDatabaseName("ix_act_trans_company_account_date");
        b.HasIndex(x => x.ReceiptId).HasDatabaseName("ix_act_trans_receipt");

        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
