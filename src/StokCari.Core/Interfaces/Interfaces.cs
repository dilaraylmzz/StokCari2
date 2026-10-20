using StokCari.Core.Dtos;
using StokCari.Core.Entities;
using StokCari.Core.Enums;

namespace StokCari.Core.Interfaces;

/// <summary>İstek sahibinin kimliği. Şirket bilgisi istemciden değil token'dan (Admin için X-Company-Id başlığından) alınır.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Role { get; }
    bool IsAdmin { get; }

    /// <summary>
    /// User: token'daki company_id. Admin: X-Company-Id başlığı (yoksa null = tüm şirketler).
    /// Kimliksiz istekte null.
    /// </summary>
    Guid? CompanyId { get; }

    Guid RequireCompanyId();
    Guid RequireUserId();
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);
    Task RemoveAsync(string key);
    Task<long> GetVersionAsync(string scope);
    Task BumpVersionAsync(string scope);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
}

public interface ICompanyService
{
    Task<PagedResult<CompanyDto>> ListAsync(PagedQuery query, CancellationToken ct);
    Task<CompanyDto> GetAsync(Guid id, CancellationToken ct);
    Task<CompanyDto> CreateAsync(CompanyRequest request, CancellationToken ct);
    Task<CompanyDto> UpdateAsync(Guid id, CompanyRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(UserListQuery query, CancellationToken ct);
    Task<UserDto> GetAsync(Guid id, CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public interface IStockService
{
    Task<PagedResult<StockDto>> ListAsync(PagedQuery query, CancellationToken ct);
    Task<StockDto> GetAsync(Guid id, CancellationToken ct);
    Task<StockDto> CreateAsync(StockRequest request, CancellationToken ct);
    Task<StockDto> UpdateAsync(Guid id, StockRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<BalanceCheckDto> CheckBalanceAsync(Guid id, CancellationToken ct);
}

public interface IAccountService
{
    Task<PagedResult<AccountDto>> ListAsync(AccountListQuery query, CancellationToken ct);
    Task<AccountDto> GetAsync(Guid id, CancellationToken ct);
    Task<AccountDto> CreateAsync(AccountRequest request, CancellationToken ct);
    Task<AccountDto> UpdateAsync(Guid id, AccountRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<BalanceCheckDto> CheckBalanceAsync(Guid id, CancellationToken ct);
}

public interface IReceiptService
{
    Task<PagedResult<ReceiptDto>> ListAsync(ReceiptListQuery query, CancellationToken ct);
    Task<ReceiptDto> GetAsync(Guid id, CancellationToken ct);
    Task<ReceiptDto> CreateAsync(CreateReceiptRequest request, CancellationToken ct);
    Task<ReceiptDto> UpdateAsync(Guid id, UpdateReceiptRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ReceiptDto> ApproveAsync(Guid id, CancellationToken ct);
    Task<ReceiptDto> CancelAsync(Guid id, CancellationToken ct);
}

public interface IReceiptSeriesService
{
    Task<IReadOnlyList<ReceiptSeriesDto>> ListAsync(ReceiptType? type, CancellationToken ct);
    Task<ReceiptSeriesDto> CreateAsync(CreateReceiptSeriesRequest request, CancellationToken ct);
}
