using System.ComponentModel.DataAnnotations;
using StokCari.Core.Enums;

namespace StokCari.Core.Dtos;

public class ReceiptDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = default!;
    public Guid StockId { get; set; }
    public string StockName { get; set; } = default!;
    public ReceiptType Type { get; set; }
    public ReceiptStatus Status { get; set; }
    public string Series { get; set; } = default!;
    public int Number { get; set; }
    public string ReceiptNo { get; set; } = default!;
    public DateOnly ReceiptDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateReceiptRequest
{
    [Required]
    public Guid AccountId { get; set; }

    [Required]
    public Guid StockId { get; set; }

    [EnumDataType(typeof(ReceiptType))]
    public ReceiptType Type { get; set; }

    /// <summary>Şirket için tanımlı ve fişin türüne ait bir seri seçilir (GET /api/receipt-series?type=...); numara seri bazında otomatik artar.</summary>
    [Required, StringLength(10)]
    public string Series { get; set; } = default!;

    [Required]
    public DateOnly ReceiptDate { get; set; }

    [Range(0.001, 999999999.0)]
    public decimal Quantity { get; set; }

    [Range(0.01, 999999999.0)]
    public decimal UnitPrice { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

/// <summary>Yalnızca Draft fiş güncellenebilir; tür, seri ve numara değişmez.</summary>
public class UpdateReceiptRequest
{
    [Required]
    public Guid AccountId { get; set; }

    [Required]
    public Guid StockId { get; set; }

    [Required]
    public DateOnly ReceiptDate { get; set; }

    [Range(0.001, 999999999.0)]
    public decimal Quantity { get; set; }

    [Range(0.01, 999999999.0)]
    public decimal UnitPrice { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class ReceiptListQuery : PagedQuery
{
    [EnumDataType(typeof(ReceiptStatus))]
    public ReceiptStatus? Status { get; set; }

    [EnumDataType(typeof(ReceiptType))]
    public ReceiptType? Type { get; set; }

    public Guid? AccountId { get; set; }
    public Guid? StockId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
