using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StokCari.Core.Enums;
using StokCari.Core.Dtos;
using StokCari.Core.Entities;
using StokCari.Core.Exceptions;
using StokCari.Core.Interfaces;
using StokCari.Data;

namespace StokCari.Services.Implementations;

public class UserService(AppDbContext db, IMapper mapper, ICurrentUser currentUser) : IUserService
{
    public async Task<PagedResult<UserDto>> ListAsync(UserListQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var p = SearchHelper.Pattern(q.Search.Trim());
            query = query.Where(x => EF.Functions.ILike(x.Username, p) || EF.Functions.ILike(x.Email, p));
        }

        if (q.Role.HasValue)
            query = query.Where(x => x.Role == q.Role.Value);

        query = (q.SortBy?.ToLowerInvariant(), q.Desc) switch
        {
            ("email", false) => query.OrderBy(x => x.Email).ThenBy(x => x.Id),
            ("email", true) => query.OrderByDescending(x => x.Email).ThenBy(x => x.Id),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            (_, true) => query.OrderByDescending(x => x.Username).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.Username).ThenBy(x => x.Id)
        };

        var total = await query.CountAsync(ct);
        var entities = await query.Page(q).ToListAsync(ct);
        return new PagedResult<UserDto>(mapper.Map<List<UserDto>>(entities), total, q.PageNumber, q.PageSize);
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");
        return mapper.Map<UserDto>(entity);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        await ValidateRoleAndCompanyAsync(request.Role, request.CompanyId, ct);

        var username = request.Username.Trim();
        var usernameMatch = SearchHelper.Exact(username);
        // Kullanıcı adı tüm sistemde benzersizdir; Admin X-Company-Id gönderdiğinde şirket filtresi diğer kullanıcıları gizlemesin.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => !x.IsDeleted && EF.Functions.ILike(x.Username, usernameMatch), ct))
            throw new ConflictException($"'{username}' kullanıcı adı zaten kullanılıyor.");

        var entity = new User
        {
            Username = username,
            Email = request.Email.Trim(),
            PasswordHash = PasswordHelper.Hash(request.Password),
            Role = request.Role,
            CompanyId = request.Role == UserRole.Admin ? null : request.CompanyId,
            IsActive = true
        };

        db.Users.Add(entity);
        await db.SaveChangesAsync(ct);
        return mapper.Map<UserDto>(entity);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var entity = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        await ValidateRoleAndCompanyAsync(request.Role, request.CompanyId, ct);

        entity.Email = request.Email.Trim();
        entity.Role = request.Role;
        entity.CompanyId = request.Role == UserRole.Admin ? null : request.CompanyId;
        entity.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
            entity.PasswordHash = PasswordHelper.Hash(request.NewPassword);

        await db.SaveChangesAsync(ct);
        return mapper.Map<UserDto>(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        if (currentUser.UserId == id)
            throw new ConflictException("Kendi hesabınızı silemezsiniz.");

        var entity = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        // created_by / approved_by kayıtları korunur; kullanıcı yalnızca işaretlenir ve giriş yapamaz.
        db.Users.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateRoleAndCompanyAsync(UserRole role, Guid? companyId, CancellationToken ct)
    {
        if (role == UserRole.Admin)
        {
            if (companyId.HasValue)
                throw new BadRequestException("Admin kullanıcısı bir şirkete bağlanamaz (CompanyId boş olmalı).");
            return;
        }

        if (!companyId.HasValue)
            throw new BadRequestException("User rolündeki kullanıcı için CompanyId zorunludur.");

        // Admin başlık göndermemişse tüm şirketler görünür; gönderdiyse yalnızca o şirket.
        var exists = await db.Companies.AnyAsync(c => c.Id == companyId.Value, ct);
        if (!exists)
            throw new BadRequestException("Belirtilen şirket bulunamadı.");
    }
}
