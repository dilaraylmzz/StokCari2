using Microsoft.AspNetCore.Identity;
using StokCari.Core.Dtos;
using StokCari.Core.Entities;

namespace StokCari.Services.Implementations;

internal static class PasswordHelper
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(string password) => Hasher.HashPassword(new User(), password);

    public static bool Verify(User user, string password) =>
        Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}

internal static class SearchHelper
{
    /// <summary>ILIKE joker karakterlerini kaçırır.</summary>
    public static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>ILIKE deseni oluşturur; kullanıcı girdisindeki %, _ ve \ karakterleri kaçırılır.</summary>
    public static string Pattern(string text) => "%" + Escape(text) + "%";

    /// <summary>Büyük/küçük harf duyarsız tam eşleşme (ILIKE, jokersiz).</summary>
    public static string Exact(string text) => Escape(text);
}

internal static class PagingExtensions
{
    public static IQueryable<T> Page<T>(this IQueryable<T> query, PagedQuery q) =>
        query.Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize);
}
