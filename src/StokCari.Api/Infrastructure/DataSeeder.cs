using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StokCari.Core.Enums;
using StokCari.Core.Entities;
using StokCari.Data;

namespace StokCari.Api.Infrastructure;

/// <summary>
/// İlk çalıştırmada bir Admin kullanıcısı oluşturur (yoksa). Seed:DemoData = true ise örnek şirket, kullanıcı, stok ve cari de eklenir.
/// Parolalar yapılandırmadan okunur; gerçek ortamda user-secrets veya ortam değişkeni kullanın.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = new PasswordHasher<User>();

        var adminUsername = config["Seed:AdminUsername"] ?? "admin";
        var adminPassword = config["Seed:AdminPassword"];

        var adminExists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Role == UserRole.Admin);
        if (adminExists)
        {
            // Demo modu mevcut bir veritabanına sonradan açılırsa DEMO şirketinin varsayılan serilerini tamamla.
            if (config.GetValue<bool>("Seed:DemoData"))
            {
                var demoCompany = await db.Companies.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Code == "DEMO" && !c.IsDeleted);
                if (demoCompany is not null)
                {
                    await AddDefaultSeriesAsync(db, demoCompany.Id);
                    await db.SaveChangesAsync();
                }
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Seed:AdminPassword tanımlı olmadığı için Admin kullanıcısı oluşturulmadı.");
            return;
        }

        var admin = new User
        {
            Username = adminUsername,
            Email = config["Seed:AdminEmail"] ?? "admin@example.com",
            Role = UserRole.Admin,
            CompanyId = null,
            IsActive = true
        };
        admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Admin kullanıcısı oluşturuldu: {Username}", adminUsername);

        if (!config.GetValue<bool>("Seed:DemoData"))
        {
            return;
        }

        var company = new Company { Code = "DEMO", Name = "Demo Şirketi", TaxNumber = "1234567890", CreatedBy = admin.Id };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var demoUser = new User
        {
            Username = "demo",
            Email = "demo@example.com",
            Role = UserRole.User,
            CompanyId = company.Id,
            IsActive = true,
            CreatedBy = admin.Id
        };
        demoUser.PasswordHash = hasher.HashPassword(demoUser, config["Seed:DemoPassword"] ?? "Demo12345!");
        db.Users.Add(demoUser);

        db.Stocks.Add(new Stock { CompanyId = company.Id, Code = "ELMA", Name = "Elma", Unit = "kg", UnitPrice = 20, CreatedBy = admin.Id });
        db.Accounts.Add(new Account { CompanyId = company.Id, Code = "AHMET", Name = "Ahmet", Type = AccountType.Customer, CreatedBy = admin.Id });
        await AddDefaultSeriesAsync(db, company.Id);

        await db.SaveChangesAsync();
        logger.LogInformation("Demo verisi oluşturuldu (şirket DEMO, kullanıcı demo).");
    }

    /// <summary>Demo şirketine satış (SAT) ve alış (ALS) serilerini tanımlar; var olanları atlar.</summary>
    private static async Task AddDefaultSeriesAsync(AppDbContext db, Guid companyId)
    {
        (string Series, ReceiptType Type)[] defaults = [("SAT", ReceiptType.Sale), ("ALS", ReceiptType.Purchase)];

        foreach (var (series, type) in defaults)
        {
            if (!await db.ReceiptSeries.IgnoreQueryFilters().AnyAsync(s => s.CompanyId == companyId && s.Series == series))
                db.ReceiptSeries.Add(new ReceiptSeries { CompanyId = companyId, Series = series, Type = type, LastNumber = 0 });
        }
    }
}
