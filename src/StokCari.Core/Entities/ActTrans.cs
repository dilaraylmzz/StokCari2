using StokCari.Core.Enums;

namespace StokCari.Core.Entities;

public class ActTrans : LedgerEntity
{
    public Guid AccountId { get; set; }
    /// <summary>Fişten üretilen harekette dolu; fişsiz hareketlerde (açılış, düzeltme vb.) boş olabilir.</summary>
    public Guid? ReceiptId { get; set; }
    /// <summary>Her zaman pozitif; yön Direction alanındadır.</summary>
    public decimal Amount { get; set; }
    public TransDirection Direction { get; set; }
    public DateOnly TransDate { get; set; }
}
