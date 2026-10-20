using System.ComponentModel.DataAnnotations;

namespace StokCari.Core.Dtos;

public class StockDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class StockRequest
{
    [Required, StringLength(50)]
    public string Code { get; set; } = default!;

    [Required, StringLength(200)]
    public string Name { get; set; } = default!;

    [Required, StringLength(20)]
    public string Unit { get; set; } = default!;

    [Range(0.0, 999999999.0)]
    public decimal UnitPrice { get; set; }
}

/// <summary>Saklanan bakiye ile geçerli (Approved) hareket toplamının karşılaştırması.</summary>
public class BalanceCheckDto
{
    public Guid Id { get; set; }
    public decimal StoredBalance { get; set; }
    public decimal CalculatedBalance { get; set; }
    public bool IsConsistent { get; set; }
}
