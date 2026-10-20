using StokCari.Core.Enums;

namespace StokCari.Core.Entities;

public class Receipt : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public Guid StockId { get; set; }
    public ReceiptType Type { get; set; } = ReceiptType.Sale;
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Draft;
    public string Series { get; set; } = default!;
    public int Number { get; set; }
    public DateOnly ReceiptDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
}
