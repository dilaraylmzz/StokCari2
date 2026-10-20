using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StokCari.Core.Enums;
using StokCari.Core.Dtos;
using StokCari.Core.Entities;
using StokCari.Core.Exceptions;
using StokCari.Core.Interfaces;
using StokCari.Data;
using StokCari.Services.Caching;

namespace StokCari.Services.Implementations;

public class AccountService(AppDbContext db, IMapper mapper, ICurrentUser user, ICacheService cache, ILogger<AccountService> logger) : IAccountService
{
    private const string Entity = CacheEntities.Account;

    public async Task<PagedResult<AccountDto>> ListAsync(AccountListQuery q, CancellationToken ct)
    {
        var version = await cache.GetVersionAsync(CacheKeys.ListScope(Entity, user.CompanyId));
        var key = CacheKeys.List(Entity, user.CompanyId, version,
            CacheKeys.Hash($"{q.PageNumber}|{q.PageSize}|{q.Search}|{q.SortBy}|{q.Desc}|{q.Type}"));

        var cached = await cache.GetAsync<PagedResult<AccountDto>>(key);
        if (cached is not null) return cached;

        var query = db.Accounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var p = SearchHelper.Pattern(q.Search.Trim());
            query = query.Where(x => EF.Functions.ILike(x.Code, p) || EF.Functions.ILike(x.Name, p));
        }

        if (q.Type.HasValue)
            query = query.Where(x => x.Type == q.Type.Value);

        query = (q.SortBy?.ToLowerInvariant(), q.Desc) switch
        {
            ("name", false) => query.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenBy(x => x.Id),
            ("balance", false) => query.OrderBy(x => x.Balance).ThenBy(x => x.Id),
            ("balance", true) => query.OrderByDescending(x => x.Balance).ThenBy(x => x.Id),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            (_, true) => query.OrderByDescending(x => x.Code).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.Code).ThenBy(x => x.Id)
        };

        var total = await query.CountAsync(ct);
        var entities = await query.Page(q).ToListAsync(ct);
        var result = new PagedResult<AccountDto>(mapper.Map<List<AccountDto>>(entities), total, q.PageNumber, q.PageSize);

        await cache.SetAsync(key, result);
        return result;
    }

    public async Task<AccountDto> GetAsync(Guid id, CancellationToken ct)
    {
        var key = CacheKeys.Item(Entity, id);
        var cached = await cache.GetAsync<AccountDto>(key);

        if (cached is not null && (user.CompanyId is null || cached.CompanyId == user.CompanyId))
            return cached;

        var entity = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Cari bulunamadı.");

        var dto = mapper.Map<AccountDto>(entity);
        await cache.SetAsync(key, dto);
        return dto;
    }

    public async Task<AccountDto> CreateAsync(AccountRequest request, CancellationToken ct)
    {
        var companyId = user.RequireCompanyId();
        var code = request.Code.Trim();

        if (await db.Accounts.AnyAsync(x => x.CompanyId == companyId && x.Code == code, ct))
            throw new ConflictException($"'{code}' kodlu bir cari zaten var.");

        var entity = new Account
        {
            CompanyId = companyId,
            Code = code,
            Balance = 0 // bakiye yalnızca onaylı fişlerle değişir
        };
        Apply(entity, request);

        db.Accounts.Add(entity);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEntityAsync(Entity, companyId, entity.Id);

        logger.LogInformation("Cari oluşturuldu {AccountId} {Code}", entity.Id, entity.Code);
        return mapper.Map<AccountDto>(entity);
    }

    public async Task<AccountDto> UpdateAsync(Guid id, AccountRequest request, CancellationToken ct)
    {
        var entity = await db.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Cari bulunamadı.");

        var code = request.Code.Trim();
        if (code != entity.Code &&
            await db.Accounts.AnyAsync(x => x.CompanyId == entity.CompanyId && x.Code == code && x.Id != id, ct))
            throw new ConflictException($"'{code}' kodlu bir cari zaten var.");

        entity.Code = code;
        Apply(entity, request);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Cari kartı başka bir işlem tarafından güncellendi, lütfen tekrar deneyin.");
        }

        await cache.InvalidateEntityAsync(Entity, entity.CompanyId, entity.Id);
        return mapper.Map<AccountDto>(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Cari bulunamadı.");

        if (await db.Receipts.AnyAsync(r => r.AccountId == id && r.Status == ReceiptStatus.Draft, ct))
            throw new ConflictException("Taslak fişte kullanılan cari silinemez.");

        if (entity.Balance != 0)
            throw new ConflictException("Bakiyesi sıfır olmayan cari silinemez.");

        db.Accounts.Remove(entity); // soft delete
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEntityAsync(Entity, entity.CompanyId, entity.Id);

        logger.LogInformation("Cari silindi (soft) {AccountId}", id);
    }

    public async Task<BalanceCheckDto> CheckBalanceAsync(Guid id, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Cari bulunamadı.");

        // Yalnızca Approved fişlerin hareketleri sayılır. Borç (+), Alacak (-).
        var debit = await db.EffectiveActTrans()
            .Where(t => t.AccountId == id && t.Direction == TransDirection.Debit)
            .SumAsync(t => t.Amount, ct);
        var credit = await db.EffectiveActTrans()
            .Where(t => t.AccountId == id && t.Direction == TransDirection.Credit)
            .SumAsync(t => t.Amount, ct);
        var calculated = debit - credit;

        return new BalanceCheckDto
        {
            Id = id,
            StoredBalance = account.Balance,
            CalculatedBalance = calculated,
            IsConsistent = account.Balance == calculated
        };
    }

    private static void Apply(Account entity, AccountRequest r)
    {
        entity.Name = r.Name.Trim();
        entity.Type = r.Type;
        entity.TaxNumber = r.TaxNumber?.Trim();
        entity.Phone = r.Phone?.Trim();
        entity.Email = r.Email?.Trim();
        entity.Address = r.Address?.Trim();
    }
}
