using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Escribe el historial de estados al guardar (Plan J, J14): cada cambio de
/// <see cref="MemberProfile.Status"/> va a MemberStatusHistories y cada alta o cambio de
/// <see cref="MembershipSubscription.SubscriptionStatus"/> a MembershipStatusHistories.
/// </summary>
/// <remarks>
/// POR QUÉ AQUÍ. Tres sitios escribían el historial a mano (actualizar estado desde el Admin,
/// cancelar membresía y el job de cancelaciones programadas) y otros cuatro cambiaban estados sin
/// dejar rastro (activación del alta, facturación recurrente, subida y bajada de nivel). Al
/// guardar no se escapa ninguno, tampoco el que se escriba mañana.
///
/// COMPARA VALORES, NO <c>IsModified</c>: un <c>Update(entidad)</c> marca todas las columnas como
/// modificadas aunque no cambien (mismo criterio que SecurityFactorsInterceptor).
///
/// NO DUPLICA: si el mismo guardado ya trae una MemberStatusHistory añadida para ese miembro y ese
/// estado nuevo, es la del manejador, con su motivo, y se respeta.
///
/// NO VE ExecuteUpdate NI SQL CRUDO: lo vigila EstadosSinInterceptorTests.
///
/// HORA: DateTime.Now, hora de servidor, como AuditInterceptor; el interceptor se crea en
/// AppDbContext.OnConfiguring, sin contenedor (J25).
/// </remarks>
public sealed class StatusHistoryInterceptor : SaveChangesInterceptor
{
    internal const string SystemActor = "system";
    private const int ActorMaxLength = 100;

    private readonly Func<DateTime> _now;

    /// <param name="now">Solo para pruebas. Por defecto <see cref="DateTime.Now"/>.</param>
    public StatusHistoryInterceptor(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) Record(context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Record(context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Record(DbContext context)
    {
        // Se materializa antes de añadir filas: no se puede tocar el ChangeTracker mientras se recorre.
        var entries = context.ChangeTracker.Entries().ToList();
        var now     = _now();

        var writtenByHandler = entries
            .Where(e => e.State == EntityState.Added && e.Entity is MemberStatusHistory)
            .Select(e => (MemberStatusHistory)e.Entity)
            .Select(h => (h.MemberId, h.NewStatus))
            .ToHashSet();

        var accountRows    = new List<MemberStatusHistory>();
        var membershipRows = new List<MembershipStatusHistory>();

        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case MemberProfile profile when entry.State == EntityState.Modified:
                {
                    var status = entry.Property(nameof(MemberProfile.Status));
                    if (!ValueChanged(status) || writtenByHandler.Contains((profile.MemberId, profile.Status)))
                        break;

                    accountRows.Add(new MemberStatusHistory
                    {
                        MemberId     = profile.MemberId,
                        OldStatus    = (MemberAccountStatus)status.OriginalValue!,
                        NewStatus    = profile.Status,
                        ChangedAt    = now,
                        CreationDate = now,
                        CreatedBy    = Actor(profile.LastUpdateBy)
                    });
                    break;
                }

                case MembershipSubscription subscription when entry.State == EntityState.Added:
                    membershipRows.Add(Row(subscription, null, now));
                    break;

                case MembershipSubscription subscription when entry.State == EntityState.Modified:
                {
                    var status = entry.Property(nameof(MembershipSubscription.SubscriptionStatus));
                    if (!ValueChanged(status)) break;

                    membershipRows.Add(Row(subscription, (MembershipStatus)status.OriginalValue!, now));
                    break;
                }
            }
        }

        if (accountRows.Count > 0)    context.Set<MemberStatusHistory>().AddRange(accountRows);
        if (membershipRows.Count > 0) context.Set<MembershipStatusHistory>().AddRange(membershipRows);
    }

    private static MembershipStatusHistory Row(MembershipSubscription s, MembershipStatus? oldStatus, DateTime now) => new()
    {
        MemberId          = s.MemberId,
        SubscriptionId    = s.Id,
        MembershipLevelId = s.MembershipLevelId,
        OldStatus         = oldStatus,
        NewStatus         = s.SubscriptionStatus,
        ChangedAt         = now,
        CreationDate      = now,
        CreatedBy         = Actor(s.LastUpdateBy ?? s.CreatedBy)
    };

    private static bool ValueChanged(PropertyEntry property) =>
        property.IsModified && !Equals(property.OriginalValue, property.CurrentValue);

    private static string Actor(string? who)
    {
        var actor = string.IsNullOrWhiteSpace(who) ? SystemActor : who.Trim();
        return actor.Length <= ActorMaxLength ? actor : actor[..ActorMaxLength];
    }
}
