using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StokCari.Core.Enums;
using StokCari.Core.Dtos;
using StokCari.Core.Entities;
using StokCari.Core.Exceptions;
using StokCari.Core.Interfaces;
using StokCari.Data;

namespace StokCari.Services.Implementations;

public class CompanyService(AppDbContext db, IMapper mapper) : ICompanyService
{
    public async Task<PagedResult<CompanyDto>> ListAsync(PagedQuery q, CancellationToken ct)
    {
        var query = db.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var p = SearchHelper.Pattern(q.Search.Trim());
            query = query.Where(x => EF.Functions.ILike(x.Code, p) || EF.Functions.ILike(x.Name, p));
        }

        query = (q.SortBy?.ToLowerInvariant(), q.Desc) switch
        {
            ("name", false) => query.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenBy(x => x.Id),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            (_, true) => query.OrderByDescending(x => x.Code).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.Code).ThenBy(x => x.Id)
        };

        var total = await query.CountAsync(ct);
        var entities = await query.Page(q).ToListAsync(ct);
        return new PagedResult<CompanyDto>(mapper.Map<List<CompanyDto>>(entities), total, q.PageNumber, q.PageSize);
    }

    public async Task<CompanyDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Şirket bulunamadı.");
        return mapper.Map<CompanyDto>(entity);
    }

    public async Task<CompanyDto> CreateAsync(CompanyRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim();
        if (await CodeExistsAsync(code, excludeId: null, ct))
            throw new ConflictException($"'{code}' kodlu bir şirket zaten var.");

        var entity = new Company
        {
            Code = code,
            Name = request.Name.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            IsActive = request.IsActive
        };

        db.Companies.Add(entity);
        await db.SaveChangesAsync(ct);
        return mapper.Map<CompanyDto>(entity);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, CompanyRequest request, CancellationToken ct)
    {
        var entity = await db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Şirket bulunamadı.");

        var code = request.Code.Trim();
        if (code != entity.Code && await CodeExistsAsync(code, excludeId: id, ct))
            throw new ConflictException($"'{code}' kodlu bir şirket zaten var.");

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.TaxNumber = request.TaxNumber?.Trim();
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return mapper.Map<CompanyDto>(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Şirket bulunamadı.");

        // Admin başka bir şirketin başlığıyla çalışıyor olsa bile doğru sonucu almak için filtreler kapatılıp açıkça sorgulanır.
        var hasActiveUsers = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.CompanyId == id && !u.IsDeleted && u.IsActive, ct);
        if (hasActiveUsers)
            throw new ConflictException("Aktif kullanıcısı olan şirket silinemez.");

        var hasDraftReceipts = await db.Receipts.IgnoreQueryFilters()
            .AnyAsync(r => r.CompanyId == id && !r.IsDeleted && r.Status == ReceiptStatus.Draft, ct);
        if (hasDraftReceipts)
            throw new ConflictException("Taslak fişi olan şirket silinemez.");

        db.Companies.Remove(entity); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Şirket kodu tüm sistemde benzersizdir. Admin X-Company-Id gönderdiğinde şirket filtresi diğer şirketleri
    /// gizleyeceği için filtreler kapatılıp silinmemiş kayıtlar açıkça sorgulanır.
    /// </summary>
    private Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct) =>
        db.Companies.IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && x.Code == code && x.Id != excludeId, ct);
}
