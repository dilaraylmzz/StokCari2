using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StokCari.Core.Entities;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Infrastructure;

public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var jwt = options.Value;
        var expiresAt = DateTime.UtcNow.AddMinutes(jwt.ExpiryMinutes);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["name"] = user.Username,
            ["role"] = user.Role.ToString()
        };

        // Şirket bilgisi token'da taşınır; istemciden alınmaz. Admin için claim yoktur.
        if (user.CompanyId.HasValue)
        {
            claims["company_id"] = user.CompanyId.Value.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Claims = claims,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAt);
    }
}
