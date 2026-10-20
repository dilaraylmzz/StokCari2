namespace StokCari.Core.Entities;

public class Company : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public bool IsActive { get; set; } = true;
}
