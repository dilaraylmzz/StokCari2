using System.ComponentModel.DataAnnotations;
using StokCari.Core.Enums;

namespace StokCari.Core.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    [Required, StringLength(100)]
    public string Username { get; set; } = default!;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = default!;

    [Required, StringLength(200, MinimumLength = 8)]
    public string Password { get; set; } = default!;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; }

    /// <summary>Role = User ise zorunlu, Admin için boş olmalıdır.</summary>
    public Guid? CompanyId { get; set; }
}

public class UpdateUserRequest
{
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = default!;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; }

    public Guid? CompanyId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Doluysa parola değiştirilir.</summary>
    [StringLength(200, MinimumLength = 8)]
    public string? NewPassword { get; set; }
}

public class UserListQuery : PagedQuery
{
    [EnumDataType(typeof(UserRole))]
    public UserRole? Role { get; set; }
}
