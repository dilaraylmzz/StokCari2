using StokCari.Core.Enums;

namespace StokCari.Core.Entities;

public class User : BaseEntity
{
    /// <summary>Role = User ise zorunlu, Admin için NULL.</summary>
    public Guid? CompanyId { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
}
