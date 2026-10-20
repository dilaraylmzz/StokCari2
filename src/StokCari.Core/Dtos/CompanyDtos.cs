using System.ComponentModel.DataAnnotations;

namespace StokCari.Core.Dtos;

public class CompanyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CompanyRequest
{
    [Required, StringLength(50)]
    public string Code { get; set; } = default!;

    [Required, StringLength(200)]
    public string Name { get; set; } = default!;

    [StringLength(20)]
    public string? TaxNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
