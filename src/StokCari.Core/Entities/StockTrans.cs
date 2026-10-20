using StokCari.Core.Enums;

namespace StokCari.Core.Entities;

public class StockTrans : LedgerEntity
{
    public Guid StockId { get; set; }
    /// <summary>Fişten üretilen harekette dolu; fişsiz hareketlerde (açılış, düzeltme vb.) boş olabilir.</summary>
    public Guid? ReceiptId { get; set; }
    /// <summary>Her zaman pozitif; yön Direction alanındadır.</summary>
    public decimal Quantity { get; set; }
    /// <summary>In (giriş) veya Out (çıkış).</summary>
    public StockDirection Direction { get; set; }
    public DateOnly TransDate { get; set; }
}
