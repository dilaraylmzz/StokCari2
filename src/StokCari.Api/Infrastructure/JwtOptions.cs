namespace StokCari.Api.Infrastructure;

public class JwtOptions
{
    public string Issuer { get; set; } = "StokCari.Api";
    public string Audience { get; set; } = "StokCari.Client";

    /// <summary>En az 32 karakter. Gerçek değer user-secrets veya ortam değişkeniyle verilmelidir.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
