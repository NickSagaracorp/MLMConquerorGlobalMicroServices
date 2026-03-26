using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyAuditFields(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.Now;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditChangesStringKey stringKeyEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    stringKeyEntity.CreationDate = now;
                    stringKeyEntity.LastUpdateDate = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    stringKeyEntity.LastUpdateDate = now;
                }
            }

            if (entry.Entity is AuditChangesIntKey intKeyEntity)
            {
                if (entry.State == EntityState.Added)
                    intKeyEntity.CreationDate = now;
                else if (entry.State == EntityState.Modified)
                    intKeyEntity.LastUpdateDate = now;
            }

            if (entry.Entity is AuditChangesLongKey longKeyEntity && entry.State == EntityState.Added)
                longKeyEntity.CreationDate = now;
        }
    }
}
