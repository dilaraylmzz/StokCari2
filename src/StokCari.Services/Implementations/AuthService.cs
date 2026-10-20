using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StokCari.Core.Enums;
using StokCari.Core.Dtos;
using StokCari.Core.Exceptions;
using StokCari.Core.Interfaces;
using StokCari.Data;

namespace StokCari.Services.Implementations;

public class AuthService(AppDbContext db, ITokenService tokens, ILogger<AuthService> logger) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        // Silinmiş kullanıcılar global query filter ile zaten dışarıda kalır.
        // ILIKE: ToLower() mevcut kültürde (ör. Türkçe I/ı) SQL LOWER() ile örtüşmeyebilir.
        var usernameMatch = SearchHelper.Exact(request.Username);
        var user = await db.Users.FirstOrDefaultAsync(u => EF.Functions.ILike(u.Username, usernameMatch), ct);

        if (user is null || !user.IsActive || !PasswordHelper.Verify(user, request.Password))
        {
            logger.LogWarning("Başarısız giriş denemesi: {Username}", request.Username);
            throw new UnauthorizedException("Kullanıcı adı veya parola hatalı.");
        }

        // Silinen veya pasif şirketin kullanıcıları giriş yapamaz.
        if (user.Role == UserRole.User)
        {
            var companyActive = await db.Companies.AnyAsync(c => c.Id == user.CompanyId && c.IsActive, ct);
            if (!companyActive)
            {
                logger.LogWarning("Şirketi pasif/silinmiş kullanıcı giriş denedi: {Username}", user.Username);
                throw new UnauthorizedException("Kullanıcı adı veya parola hatalı.");
            }
        }

        var (token, expiresAt) = tokens.CreateToken(user);
        logger.LogInformation("Kullanıcı giriş yaptı: {UserId} {Role}", user.Id, user.Role);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role,
            CompanyId = user.CompanyId
        };
    }
}
