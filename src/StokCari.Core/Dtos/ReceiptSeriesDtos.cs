using System.ComponentModel.DataAnnotations;
using StokCari.Core.Enums;

namespace StokCari.Core.Dtos;

public sealed record ReceiptSeriesDto(string Series, ReceiptType Type, int LastNumber);

public sealed class CreateReceiptSeriesRequest
{
    [Required, StringLength(10, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Seri yalnızca harf ve rakam içerebilir.")]
    public string Series { get; set; } = default!;

    /// <summary>Bu seriyle kesilebilecek fiş türü (Sale veya Purchase).</summary>
    [EnumDataType(typeof(ReceiptType))]
    public ReceiptType Type { get; set; }
}
