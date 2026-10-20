using System.ComponentModel.DataAnnotations;

namespace StokCari.Core.Dtos;

public class PagedQuery
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Üst sınır 100'dür.</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [StringLength(100)]
    public string? Search { get; set; }

    public string? SortBy { get; set; }
    public bool Desc { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
