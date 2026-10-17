using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Membership;

/// <summary>
/// Un cambio de estado de una suscripción (Plan J, J13). Hermana de MemberStatusHistory, que es
/// el estado de la CUENTA: aquí va el de la MEMBRESÍA. La escribe StatusHistoryInterceptor al
/// guardar; nadie la escribe a mano.
/// </summary>
public class MembershipStatusHistory : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;

    /// <summary>MembershipSubscription.Id: una subida de nivel crea otra suscripción.</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    public int MembershipLevelId { get; set; }

    /// <summary>Null cuando la fila registra la creación de la suscripción.</summary>
    public MembershipStatus? OldStatus { get; set; }

    public MembershipStatus NewStatus { get; set; }

    /// <summary>Hora de servidor.</summary>
    public DateTime ChangedAt { get; set; }
}
