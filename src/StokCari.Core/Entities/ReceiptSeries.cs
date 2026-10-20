using StokCari.Core.Enums;

namespace StokCari.Core.Entities;

/// <summary>
/// Şirket bazında Admin'in tanımladığı fiş serisi ve numara sayacı. Her seri tek bir fiş türüne aittir
/// (ör. SAT = Satış, ALS = Alış); böylece satış ve alış fişleri ayrı ve kesintisiz numaralanır. Soft delete uygulanmaz.
/// </summary>
public class ReceiptSeries : ITenantEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public string Series { get; set; } = default!;
    /// <summary>Bu seriyle kesilebilecek fiş türü.</summary>
    public ReceiptType Type { get; set; }
    public int LastNumber { get; set; }
    public uint RowVersion { get; set; } // PostgreSQL xmin sistem kolonu (optimistic concurrency)
}
