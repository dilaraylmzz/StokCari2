using System.Security.Cryptography;
using System.Text;
using StokCari.Core.Interfaces;

namespace StokCari.Services.Caching;

public static class CacheEntities
{
    public const string Stock = "stock";
    public const string Account = "account";
}

/// <summary>
/// Anahtar stratejisi:
///  - Tek kayıt:  {entity}:item:{id}
///  - Liste:      {entity}:list:{scope}:v{version}:{hash}   (scope = şirket veya "all")
/// Liste anahtarları sayfa/filtre/sıralama kombinasyonuyla çoğalır. Bunları tek tek silmek yerine
/// şirket bazlı bir versiyon sayacı artırılır; eski versiyonlu anahtarlar bir daha okunmaz ve TTL ile kendiliğinden silinir.
/// </summary>
public static class CacheKeys
{
    public static string Item(string entity, Guid id) => $"{entity}:item:{id:N}";

    public static string ListScope(string entity, Guid? companyId) =>
        $"{entity}:{(companyId.HasValue ? companyId.Value.ToString("N") : "all")}";

    public static string List(string entity, Guid? companyId, long version, string hash) =>
        $"{entity}:list:{ListScope(entity, companyId)}:v{version}:{hash}";

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];

    /// <summary>
    /// Kayıt değişince (CRUD, fiş onayı/iptali) çağrılır. MUTLAKA transaction commit edildikten sonra çağrılmalıdır;
    /// aksi halde başka bir istek eski veriyi yeniden cache'e yazabilir.
    /// </summary>
    public static async Task InvalidateEntityAsync(this ICacheService cache, string entity, Guid companyId, Guid? id = null)
    {
        if (id.HasValue)
        {
            await cache.RemoveAsync(Item(entity, id.Value));
        }

        await cache.BumpVersionAsync(ListScope(entity, companyId));
        await cache.BumpVersionAsync(ListScope(entity, null)); // Admin'in "tüm şirketler" listesi
    }
}
