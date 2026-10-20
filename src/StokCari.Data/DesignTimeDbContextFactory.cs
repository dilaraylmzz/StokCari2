using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StokCari.Core.Interfaces;

namespace StokCari.Data;

/// <summary>
/// "dotnet ef" komutları için. Bağlantı dizesi STOKCARI_CONNECTION ortam değişkeninden okunur.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("STOKCARI_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=stokcari;Username=postgres;Password=CHANGE_ME";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new AppDbContext(options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string? Role => null;
        public bool IsAdmin => false;
        public Guid? CompanyId => null;
        public Guid RequireCompanyId() => throw new NotSupportedException();
        public Guid RequireUserId() => throw new NotSupportedException();
    }
}
