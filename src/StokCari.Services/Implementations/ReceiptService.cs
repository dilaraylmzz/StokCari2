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

public class ReceiptService(AppDbContext db, IMapper mapper, ICurrentUser user, ICacheService cache, ILogger<ReceiptService> logger) : IReceiptService
{
    // ---------------------------------------------------------------- Listeleme / okuma

    public async Task<PagedResult<ReceiptDto>> ListAsync(ReceiptListQuery q, CancellationToken ct)
    {
        var query = db.Receipts.AsNoTracking();

        if (q.Status.HasValue) query = query.Where(x => x.Status == q.Status.Value);
        if (q.Type.HasValue) query = query.Where(x => x.Type == q.Type.Value);
        if (q.AccountId.HasValue) query = query.Where(x => x.AccountId == q.AccountId.Value);
        if (q.StockId.HasValue) query = query.Where(x => x.StockId == q.StockId.Value);
        if (q.DateFrom.HasValue) query = query.Where(x => x.ReceiptDate >= q.DateFrom.Value);
        if (q.DateTo.HasValue) query = query.Where(x => x.ReceiptDate <= q.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var p = SearchHelper.Pattern(q.Search.Trim());
            query = query.Where(x => EF.Functions.ILike(x.Series, p) || (x.Description != null && EF.Functions.ILike(x.Description, p)));
        }

        query = (q.SortBy?.ToLowerInvariant(), q.Desc) switch
        {
            ("number", false) => query.OrderBy(x => x.Series).ThenBy(x => x.Number),
            ("number", true) => query.OrderByDescending(x => x.Series).ThenByDescending(x => x.Number),
            ("total", false) => query.OrderBy(x => x.TotalAmount).ThenBy(x => x.Id),
            ("total", true) => query.OrderByDescending(x => x.TotalAmount).ThenBy(x => x.Id),
            ("date", false) => query.OrderBy(x => x.ReceiptDate).ThenBy(x => x.Number),
            _ => query.OrderByDescending(x => x.ReceiptDate).ThenByDescending(x => x.Number) // varsayılan: en yeni önce
        };

        var total = await query.CountAsync(ct);
        var rows = await query.Page(q).ToListAsync(ct);
        var items = await ToDtosAsync(rows, ct);

        return new PagedResult<ReceiptDto>(items, total, q.PageNumber, q.PageSize);
    }

    public async Task<ReceiptDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Fiş bulunamadı.");

        return (await ToDtosAsync([entity], ct)).Single();
    }

    // ---------------------------------------------------------------- Oluşturma / düzenleme / silme (yalnızca Draft)

    public async Task<ReceiptDto> CreateAsync(CreateReceiptRequest request, CancellationToken ct)
    {
        var companyId = user.RequireCompanyId();
        user.RequireUserId();

        await EnsureAccountAndStockExistAsync(request.AccountId, request.StockId, ct);

        var series = request.Series.Trim().ToUpperInvariant();

        // Numara sayacı ile fiş aynı SaveChanges'te (tek transaction) kaydedilir: fiş kaydedilemezse numara da harcanmaz.
        // Aynı seriye eşzamanlı istek gelirse sayaç çakışır; değişiklikler bırakılıp birkaç kez yeniden denenir.
        const int maxAttempts = 5;
        Receipt entity;

        for (var attempt = 1; ; attempt++)
        {
            var number = await NextNumberAsync(companyId, series, request.Type, ct);

            entity = new Receipt
            {
                CompanyId = companyId,
                AccountId = request.AccountId,
                StockId = request.StockId,
                Type = request.Type,
                Status = ReceiptStatus.Draft,
                Series = series,
                Number = number,
                ReceiptDate = request.ReceiptDate,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                TotalAmount = CalculateTotal(request.Quantity, request.UnitPrice),
                Description = request.Description?.Trim()
            };

            db.Receipts.Add(entity);

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (IsSeriesConflict(ex) && attempt < maxAttempts)
            {
                logger.LogWarning("Fiş numarası çakışması, yeniden deneniyor {Series} (deneme {Attempt})", series, attempt);
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (IsSeriesConflict(ex))
            {
                throw new ConflictException("Fiş numarası üretilemedi, lütfen tekrar deneyin.");
            }
        }

        logger.LogInformation("Taslak fiş oluşturuldu {ReceiptId} {Series}-{Number}", entity.Id, entity.Series, entity.Number);
        return (await ToDtosAsync([entity], ct)).Single();
    }

    public async Task<ReceiptDto> UpdateAsync(Guid id, UpdateReceiptRequest request, CancellationToken ct)
    {
        var entity = await db.Receipts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Fiş bulunamadı.");

        if (entity.Status != ReceiptStatus.Draft)
            throw new ConflictException("Yalnızca taslak fiş düzenlenebilir.");

        await EnsureAccountAndStockExistAsync(request.AccountId, request.StockId, ct);

        // Tür değiştirilemez: seri ve numara türe bağlıdır. Tür yanlışsa taslak silinip doğru seriyle yeniden oluşturulur.
        entity.AccountId = request.AccountId;
        entity.StockId = request.StockId;
        entity.ReceiptDate = request.ReceiptDate;
        entity.Quantity = request.Quantity;
        entity.UnitPrice = request.UnitPrice;
        entity.TotalAmount = CalculateTotal(request.Quantity, request.UnitPrice);
        entity.Description = request.Description?.Trim();

        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([entity], ct)).Single();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Receipts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Fiş bulunamadı.");

        if (entity.Status != ReceiptStatus.Draft)
        {
            throw new ConflictException(
                entity.Status == ReceiptStatus.Approved
                    ? "Onaylı fiş silinemez, iptal edilmelidir."
                    : "İptal edilmiş fiş kapanmış kayıttır, silinemez.");
        }

        db.Receipts.Remove(entity); // soft delete
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Taslak fiş silindi (soft) {ReceiptId}", id);
    }

    // ---------------------------------------------------------------- Onay

    public async Task<ReceiptDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        var userId = user.RequireUserId();
        logger.LogInformation("Fiş onayı başladı {ReceiptId}", id);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var receipt = await db.Receipts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Fiş bulunamadı.");

        if (receipt.Status != ReceiptStatus.Draft)
            throw new ConflictException("Yalnızca taslak fiş onaylanabilir.");

        // Onay kararı cache'ten değil veritabanından okunan güncel bakiyeye göre verilir.
        var stock = await db.Stocks.FirstOrDefaultAsync(x => x.Id == receipt.StockId, ct)
            ?? throw new ConflictException("Fişteki stok kartı bulunamadı (silinmiş olabilir).");
        var account = await db.Accounts.FirstOrDefaultAsync(x => x.Id == receipt.AccountId, ct)
            ?? throw new ConflictException("Fişteki cari kartı bulunamadı (silinmiş olabilir).");

        var isSale = receipt.Type == ReceiptType.Sale;
        var signedQuantity = isSale ? -receipt.Quantity : receipt.Quantity;

        if (isSale && stock.Balance < receipt.Quantity)
            throw new ConflictException($"Stok yetersiz. Mevcut: {stock.Balance}, istenen: {receipt.Quantity}.");

        var stockDirection = isSale ? StockDirection.Out : StockDirection.In;
        db.StockTrans.Add(new StockTrans
        {
            CompanyId = receipt.CompanyId,
            StockId = stock.Id,
            ReceiptId = receipt.Id,
            Quantity = receipt.Quantity,
            Direction = stockDirection,
            TransDate = receipt.ReceiptDate
        });
        logger.LogInformation("Stok hareketi üretildi {ReceiptId} {Direction} {Quantity}", receipt.Id, stockDirection, receipt.Quantity);

        db.ActTrans.Add(new ActTrans
        {
            CompanyId = receipt.CompanyId,
            AccountId = account.Id,
            ReceiptId = receipt.Id,
            Amount = receipt.TotalAmount,
            Direction = isSale ? TransDirection.Debit : TransDirection.Credit,
            TransDate = receipt.ReceiptDate
        });
        logger.LogInformation("Cari hareketi üretildi {ReceiptId} {Amount}", receipt.Id, receipt.TotalAmount);

        stock.Balance += signedQuantity;
        account.Balance += isSale ? receipt.TotalAmount : -receipt.TotalAmount;

        receipt.Status = ReceiptStatus.Approved;
        receipt.ApprovedAt = DateTime.UtcNow;
        receipt.ApprovedBy = userId;

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Fiş onayı eşzamanlılık çakışması nedeniyle geri alındı {ReceiptId}", id);
            throw new ConflictException("Stok veya cari bakiyesi başka bir işlem tarafından değiştirildi, lütfen tekrar deneyin.");
        }

        logger.LogInformation("Fiş onayı tamamlandı (commit) {ReceiptId}", id);

        // Cache, commit'ten SONRA geçersiz kılınır.
        await cache.InvalidateEntityAsync(CacheEntities.Stock, receipt.CompanyId, receipt.StockId);
        await cache.InvalidateEntityAsync(CacheEntities.Account, receipt.CompanyId, receipt.AccountId);

        return (await ToDtosAsync([receipt], ct)).Single();
    }

    // ---------------------------------------------------------------- İptal

    public async Task<ReceiptDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var userId = user.RequireUserId();
        logger.LogInformation("Fiş iptali başladı {ReceiptId}", id);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var receipt = await db.Receipts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Fiş bulunamadı.");

        if (receipt.Status != ReceiptStatus.Approved)
            throw new ConflictException("Yalnızca onaylı fiş iptal edilebilir.");

        // Onaydan sonra kart silinmiş olabilir (bakiyesi sıfırken); bu yüzden filtreler kapatılıp şirket kontrolü elle yapılır.
        var stock = await db.Stocks.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == receipt.StockId && x.CompanyId == receipt.CompanyId, ct)
            ?? throw new ConflictException("Fişteki stok kartı bulunamadı.");
        var account = await db.Accounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == receipt.AccountId && x.CompanyId == receipt.CompanyId, ct)
            ?? throw new ConflictException("Fişteki cari kartı bulunamadı.");

        if (stock.IsDeleted || account.IsDeleted)
            throw new ConflictException("Fişte kullanılan stok veya cari kartı silinmiş olduğu için fiş iptal edilemez.");

        var isSale = receipt.Type == ReceiptType.Sale;
        var originalQuantityEffect = isSale ? -receipt.Quantity : receipt.Quantity;
        var originalAmountEffect = isSale ? receipt.TotalAmount : -receipt.TotalAmount;

        // Onaydaki etkinin tersi uygulanır. Hareket satırlarına dokunulmaz; fiş Cancelled olunca geçersiz sayılırlar.
        stock.Balance -= originalQuantityEffect;
        account.Balance -= originalAmountEffect;

        if (stock.Balance < 0)
            throw new ConflictException("İptal, stok bakiyesini negatife düşüreceği için yapılamaz.");

        receipt.Status = ReceiptStatus.Cancelled;
        receipt.CancelledAt = DateTime.UtcNow;
        receipt.CancelledBy = userId;

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Fiş iptali eşzamanlılık çakışması nedeniyle geri alındı {ReceiptId}", id);
            throw new ConflictException("Stok veya cari bakiyesi başka bir işlem tarafından değiştirildi, lütfen tekrar deneyin.");
        }

        logger.LogInformation("Fiş iptali tamamlandı (commit) {ReceiptId}", id);

        await cache.InvalidateEntityAsync(CacheEntities.Stock, receipt.CompanyId, receipt.StockId);
        await cache.InvalidateEntityAsync(CacheEntities.Account, receipt.CompanyId, receipt.AccountId);

        return (await ToDtosAsync([receipt], ct)).Single();
    }

    // ---------------------------------------------------------------- Yardımcılar

    private static decimal CalculateTotal(decimal quantity, decimal unitPrice) =>
        Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);

    private async Task EnsureAccountAndStockExistAsync(Guid accountId, Guid stockId, CancellationToken ct)
    {
        if (accountId == Guid.Empty || stockId == Guid.Empty)
            throw new BadRequestException("Cari ve stok seçimi zorunludur.");

        // Global query filter: yalnızca aynı şirketin, silinmemiş kartları görünür.
        if (!await db.Accounts.AnyAsync(a => a.Id == accountId, ct))
            throw new BadRequestException("Seçilen cari bulunamadı.");

        if (!await db.Stocks.AnyAsync(s => s.Id == stockId, ct))
            throw new BadRequestException("Seçilen stok bulunamadı.");
    }

    /// <summary>
    /// Şirketin tanımlı serisinden sıradaki numarayı sayaç kaydına yazar (henüz kaydetmez; fişle aynı SaveChanges'te kaydedilir).
    /// Seri tanımlı olmalı ve fişin türüne ait olmalıdır (satış fişi yalnızca satış serisiyle kesilir).
    /// Eşzamanlılık: sayaç xmin ile korunur; çakışmada CreateAsync işlemi baştan dener.
    /// </summary>
    private async Task<int> NextNumberAsync(Guid companyId, string series, ReceiptType type, CancellationToken ct)
    {
        var counter = await db.ReceiptSeries
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Series == series, ct);

        if (counter is null)
            throw new BadRequestException($"'{series}' serisi şirket için tanımlı değil. Önce şirket serileri arasına eklenmelidir.");

        if (counter.Type != type)
            throw new BadRequestException($"'{series}' serisi {counter.Type} fişleri için tanımlı; {type} fişinde kullanılamaz.");

        counter.LastNumber++;
        return counter.LastNumber;
    }

    private static bool IsSeriesConflict(DbUpdateException ex) =>
        ex is DbUpdateConcurrencyException ||
        ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_receipt_series_company_series" };

    /// <summary>
    /// Geçmiş fişlerde stok/cari adının görünmeye devam etmesi için kartlar IgnoreQueryFilters ile okunur
    /// (kart sonradan soft delete edilmiş olabilir). Şirket kontrolü fişlerin kendisinden gelir, fişler zaten izole edilmiştir.
    /// </summary>
    private async Task<List<ReceiptDto>> ToDtosAsync(List<Receipt> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var stockIds = rows.Select(r => r.StockId).Distinct().ToList();
        var accountIds = rows.Select(r => r.AccountId).Distinct().ToList();

        var stockNames = await db.Stocks.IgnoreQueryFilters().AsNoTracking()
            .Where(s => stockIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var accountNames = await db.Accounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Name })
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var dtos = mapper.Map<List<ReceiptDto>>(rows);
        foreach (var dto in dtos)
        {
            dto.AccountName = accountNames.GetValueOrDefault(dto.AccountId, "-");
            dto.StockName = stockNames.GetValueOrDefault(dto.StockId, "-");
        }

        return dtos;
    }
}
