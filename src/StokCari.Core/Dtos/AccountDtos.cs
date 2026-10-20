using System.ComponentModel.DataAnnotations;
using StokCari.Core.Enums;

namespace StokCari.Core.Dtos;

public class AccountDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public AccountType Type { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AccountRequest
{
    [Required, StringLength(50)]
    public string Code { get; set; } = default!;

    [Required, StringLength(200)]
    public string Name { get; set; } = default!;

    [EnumDataType(typeof(AccountType))]
    public AccountType Type { get; set; }

    [StringLength(20)]
    public string? TaxNumber { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}

public class AccountListQuery : PagedQuery
{
    [EnumDataType(typeof(AccountType))]
    public AccountType? Type { get; set; }
}
