using StokCari.Core.Enums;

namespace StokCari.Core.Entities;

public class Account : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    /// <summary>Customer veya Supplier.</summary>
    public AccountType Type { get; set; } = AccountType.Customer;
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    /// <summary>Borç (+) / Alacak (-) bakiyesi.</summary>
    public decimal Balance { get; set; }
    public uint RowVersion { get; set; } // PostgreSQL xmin sistem kolonu (optimistic concurrency)
}
