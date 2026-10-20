using StokCari.Core.Enums;

namespace StokCari.Core.Constants;

/// <summary>[Authorize(Roles = ...)] sabit metin istediği için rol adları enum'dan türetilir.</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string User = nameof(UserRole.User);
    public const string AdminOrUser = Admin + "," + User;
}
