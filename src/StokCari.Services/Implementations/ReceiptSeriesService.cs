using StokCari.Core.Enums;
using Microsoft.EntityFrameworkCore;
using StokCari.Core.Dtos;
using StokCari.Core.Entities;
using StokCari.Core.Exceptions;
using StokCari.Core.Interfaces;
using StokCari.Data;

namespace StokCari.Services.Implementations;

public sealed class ReceiptSeriesService(AppDbContext db, ICurrentUser user) : IReceiptSeriesService
{
    public async Task<IReadOnlyList<ReceiptSeriesDto>> ListAsync(ReceiptType? type, CancellationToken ct)
    {
        var companyId = user.RequireCompanyId();
        var query = db.ReceiptSeries.AsNoTracking().Where(x => x.CompanyId == companyId);

        // Fiş oluştururken yalnızca fişin türüne uygun seriler listelenebilsin diye.
        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);

        return await query
            .OrderBy(x => x.Type).ThenBy(x => x.Series)
            .Select(x => new ReceiptSeriesDto(x.Series, x.Type, x.LastNumber))
            .ToListAsync(ct);
    }

    public async Task<ReceiptSeriesDto> CreateAsync(CreateReceiptSeriesRequest request, CancellationToken ct)
    {
        var companyId = user.RequireCompanyId();
        var series = request.Series.Trim().ToUpperInvariant();

        if (await db.ReceiptSeries.AnyAsync(x => x.CompanyId == companyId && x.Series == series, ct))
            throw new ConflictException($"'{series}' serisi bu şirkette zaten tanımlı.");

        var counter = new ReceiptSeries { CompanyId = companyId, Series = series, Type = request.Type, LastNumber = 0 };
        db.ReceiptSeries.Add(counter);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
               { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_receipt_series_company_series" })
        {
            throw new ConflictException($"'{series}' serisi bu şirkette zaten tanımlı.");
        }

        return new ReceiptSeriesDto(counter.Series, counter.Type, counter.LastNumber);
    }
}
