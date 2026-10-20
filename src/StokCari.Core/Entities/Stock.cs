namespace StokCari.Core.Entities;

public class Stock : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    /// <summary>Net stok bakiyesi (geçerli stock_trans toplamı).</summary>
    public decimal Balance { get; set; }
    public uint RowVersion { get; set; } // PostgreSQL xmin sistem kolonu (optimistic concurrency)
}
