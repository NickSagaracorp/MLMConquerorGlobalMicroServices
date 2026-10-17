using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

/// <summary>
/// Plan K (revisión del Bloque 3, C-1). El trabajador y el barrido comparten el contexto con el procesador
/// caso a caso. Si un caso lanza a mitad, lo que dejó sin guardar no puede colarse en el SaveChanges del
/// caso siguiente ni en el cierre del trozo: se suelta lo añadido y se relee lo modificado o borrado.
/// </summary>
public static class PendingChanges
{
    public static async Task DiscardAsync(DbContext db, ILogger logger)
    {
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    try
                    {
                        await entry.ReloadAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // Si ni se puede releer, se suelta: mejor perderlo de vista que guardarlo a medias.
                        logger.LogWarning(ex, "No se pudo releer {Entity} tras un fallo; se suelta del contexto.",
                            entry.Metadata.ClrType.Name);
                        entry.State = EntityState.Detached;
                    }
                    break;
            }
        }
    }
}
