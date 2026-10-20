using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StokCari.Core.Entities;
using StokCari.Core.Interfaces;

namespace StokCari.Data.Interceptors;

/// <summary>
/// - Oluşturma/güncelleme denetim alanlarını doldurur.
/// - Silme isteğini soft delete'e çevirir (is_deleted, deleted_at, deleted_by).
/// - Hareket tablolarının (ledger) güncellenmesini ve silinmesini engeller (salt eklenir).
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is LedgerEntity && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Hareket kayıtları (stock_trans, act_trans) salt eklenir; güncellenemez ve silinemez.");
            }

            if (entry.State == EntityState.Added && entry.Entity is ICreationAudit created)
            {
                created.CreatedAt = now;
                created.CreatedBy ??= userId;
            }

            if (entry.State == EntityState.Modified && entry.Entity is IAuditable)
            {
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = userId;
            }

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete)
            {
                // Fiziksel silme yerine işaretleme: önce Unchanged'a çek, sonra yalnızca ilgili alanları değiştir.
                entry.State = EntityState.Unchanged;
                entry.Property(nameof(ISoftDelete.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDelete.DeletedAt)).CurrentValue = now;
                entry.Property(nameof(ISoftDelete.DeletedBy)).CurrentValue = userId;

                if (entry.Entity is IAuditable)
                {
                    entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = userId;
                }
            }
        }
    }
}
