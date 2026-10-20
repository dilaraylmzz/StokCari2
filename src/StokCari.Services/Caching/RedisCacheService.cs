using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StokCari.Core.Interfaces;

namespace StokCari.Services.Caching;

/// <summary>
/// Cache-aside için Redis erişimi. Redis'e ulaşılamazsa istekler hata vermez, veritabanından çalışmaya devam eder.
/// </summary>
public class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Db => redis.GetDatabase();

    /// <summary>
    /// Redis'e bağlı değilsek cache işlemleri atlanır. Aksi halde her işlem zaman aşımına kadar (saniyeler) bekler
    /// ve API yavaşlar. Bağlantı geri geldiğinde cache kendiliğinden yeniden devreye girer.
    /// </summary>
    private bool IsAvailable => redis.IsConnected;

    public async Task<T?> GetAsync<T>(string key)
    {
        if (!IsAvailable) return default;

        try
        {
            var value = await Db.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions) : default;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache okunamadı: {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        if (!IsAvailable) return;

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await Db.StringSetAsync(key, json, ttl ?? DefaultTtl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache yazılamadı: {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        if (!IsAvailable) return;

        try
        {
            await Db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache silinemedi: {CacheKey}", key);
        }
    }

    public async Task<long> GetVersionAsync(string scope)
    {
        if (!IsAvailable) return 0;

        try
        {
            var value = await Db.StringGetAsync(VersionKey(scope));
            return value.HasValue && long.TryParse(value.ToString(), out var version) ? version : 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache versiyonu okunamadı: {Scope}", scope);
            return 0;
        }
    }

    public async Task BumpVersionAsync(string scope)
    {
        if (!IsAvailable) return;

        try
        {
            await Db.StringIncrementAsync(VersionKey(scope));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache versiyonu artırılamadı: {Scope}", scope);
        }
    }

    private static string VersionKey(string scope) => $"ver:{scope}";
}
