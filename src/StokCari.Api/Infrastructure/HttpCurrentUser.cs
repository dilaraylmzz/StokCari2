using StokCari.Core.Constants;
using StokCari.Core.Exceptions;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Infrastructure;

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string CompanyHeader = "X-Company-Id";

    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    private bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Role => Principal?.FindFirst("role")?.Value;

    public bool IsAdmin => Role == Roles.Admin;

    public Guid? CompanyId
    {
        get
        {
            if (!IsAuthenticated) return null; // login gibi kimliksiz istekler

            if (IsAdmin)
            {
                // Admin hedef şirketi başlıkla belirtir; yoksa tüm şirketleri görür.
                var header = accessor.HttpContext?.Request.Headers[CompanyHeader].ToString();
                return Guid.TryParse(header, out var headerCompany) ? headerCompany : null;
            }

            // User: şirket yalnızca token'dan gelir. Claim yoksa hiçbir kayıtla eşleşmeyen boş Guid döner (veri sızmaz).
            return Guid.TryParse(Principal?.FindFirst("company_id")?.Value, out var companyId)
                ? companyId
                : Guid.Empty;
        }
    }

    public Guid RequireCompanyId()
    {
        var companyId = CompanyId;

        if (companyId is null)
            throw new BadRequestException($"Bu işlem için bir şirket seçilmelidir. Admin kullanıcıları '{CompanyHeader}' başlığını göndermelidir.");

        if (companyId == Guid.Empty)
            throw new ForbiddenException("Kullanıcı bir şirkete bağlı değil.");

        return companyId.Value;
    }

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedException("Kimlik doğrulama gerekli.");
}
