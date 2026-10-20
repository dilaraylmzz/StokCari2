using System.ComponentModel.DataAnnotations;
using StokCari.Core.Enums;

namespace StokCari.Core.Dtos;

public class LoginRequest
{
    [Required, StringLength(100)]
    public string Username { get; set; } = default!;

    [Required, StringLength(200)]
    public string Password { get; set; } = default!;
}

public class LoginResponse
{
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public UserRole Role { get; set; }
    public Guid? CompanyId { get; set; }
}
