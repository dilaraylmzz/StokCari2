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

public class StockService(AppDbContext db, IMapper mapper, ICurrentUser user, ICacheService cache, ILogger<StockService> logger) : IStockService
{
    private const string Entity = CacheEntities.Stock;

    public async Task<PagedResult<StockDto>> ListAsync(PagedQuery q, CancellationToken ct)
    {
        // Versiyon sorgudan ÖNCE okunur: sorgu sırasında bir değişiklik olursa bu sonuç eski versiyonlu anahtara yazılır ve bir daha okunmaz.
        var version = await cache.GetVersionAsync(CacheKeys.ListScope(Entity, user.CompanyId));
        var key = CacheKeys.List(Entity, user.CompanyId, version,
            CacheKeys.Hash($"{q.PageNumber}|{q.PageSize}|{q.Search}|{q.SortBy}|{q.Desc}"));

        var cached = await cache.GetAsync<PagedResult<StockDto>>(key);
        if (cached is not null) return cached;

        var query = db.Stocks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var p = SearchHelper.Pattern(q.Search.Trim());
            query = query.Where(x => EF.Functions.ILike(x.Code, p) || EF.Functions.ILike(x.Name, p));
        }

        query = (q.SortBy?.ToLowerInvariant(), q.Desc) switch
        {
            ("name", false) => query.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenBy(x => x.Id),
            ("balance", false) => query.OrderBy(x => x.Balance).ThenBy(x => x.Id),
            ("balance", true) => query.OrderByDescending(x => x.Balance).ThenBy(x => x.Id),
            ("unitprice", false) => query.OrderBy(x => x.UnitPrice).ThenBy(x => x.Id),
            ("unitprice", true) => query.OrderByDescending(x => x.UnitPrice).ThenBy(x => x.Id),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            (_, true) => query.OrderByDescending(x => x.Code).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.Code).ThenBy(x => x.Id)
        };

        var total = await query.CountAsync(ct);
        var entities = await query.Page(q).ToListAsync(ct);
        var result = new PagedResult<StockDto>(mapper.Map<List<StockDto>>(entities), total, q.PageNumber, q.PageSize);

        await cache.SetAsync(key, result);
        return result;
    }

    public async Task<StockDto> GetAsync(Guid id, CancellationToken ct)
    {
        var key = CacheKeys.Item(Entity, id);
        var cached = await cache.GetAsync<StockDto>(key);

        // Cache'ten gelen kayıt için de şirket izolasyonu kontrol edilir.
        if (cached is not null && (user.CompanyId is null || cached.CompanyId == user.CompanyId))
            return cached;

        var entity = await db.Stocks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Stok bulunamadı.");

        var dto = mapper.Map<StockDto>(entity);
        await cache.SetAsync(key, dto);
        return dto;
    }

    public async Task<StockDto> CreateAsync(StockRequest request, CancellationToken ct)
    {
        var companyId = user.RequireCompanyId();
        var code = request.Code.Trim();

        if (await db.Stocks.AnyAsync(x => x.CompanyId == companyId && x.Code == code, ct))
            throw new ConflictException($"'{code}' kodlu bir stok zaten var.");

        var entity = new Stock
        {
            CompanyId = companyId,
            Code = code,
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            UnitPrice = request.UnitPrice,
            Balance = 0 // bakiye yalnızca onaylı fişlerle değişir
        };

        db.Stocks.Add(entity);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEntityAsync(Entity, companyId, entity.Id);

        logger.LogInformation("Stok oluşturuldu {StockId} {Code}", entity.Id, entity.Code);
        return mapper.Map<StockDto>(entity);
    }

    public async Task<StockDto> UpdateAsync(Guid id, StockRequest request, CancellationToken ct)
    {
        var entity = await db.Stocks.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Stok bulunamadı.");

        var code = request.Code.Trim();
        if (code != entity.Code &&
            await db.Stocks.AnyAsync(x => x.CompanyId == entity.CompanyId && x.Code == code && x.Id != id, ct))
            throw new ConflictException($"'{code}' kodlu bir stok zaten var.");

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.Unit = request.Unit.Trim();
        entity.UnitPrice = request.UnitPrice;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Stok kartı başka bir işlem tarafından güncellendi, lütfen tekrar deneyin.");
        }

        await cache.InvalidateEntityAsync(Entity, entity.CompanyId, entity.Id);
        return mapper.Map<StockDto>(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Stocks.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Stok bulunamadı.");

        if (await db.Receipts.AnyAsync(r => r.StockId == id && r.Status == ReceiptStatus.Draft, ct))
            throw new ConflictException("Taslak fişte kullanılan stok silinemez.");

        if (entity.Balance != 0)
            throw new ConflictException("Bakiyesi sıfır olmayan stok silinemez.");

        db.Stocks.Remove(entity); // soft delete
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEntityAsync(Entity, entity.CompanyId, entity.Id);

        logger.LogInformation("Stok silindi (soft) {StockId}", id);
    }

    public async Task<BalanceCheckDto> CheckBalanceAsync(Guid id, CancellationToken ct)
    {
        var stock = await db.Stocks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Stok bulunamadı.");

        // Geçerli hareketler sayılır (fişsiz + Approved fişli). Giriş (+), Çıkış (-).
        var calculated = await db.EffectiveStockTrans()
            .Where(t => t.StockId == id)
            .SumAsync(t => t.Direction == StockDirection.In ? t.Quantity : -t.Quantity, ct);

        return new BalanceCheckDto
        {
            Id = id,
            StoredBalance = stock.Balance,
            CalculatedBalance = calculated,
            IsConsistent = stock.Balance == calculated
        };
    }
}
