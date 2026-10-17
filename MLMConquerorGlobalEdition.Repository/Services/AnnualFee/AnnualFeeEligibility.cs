using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// Lo que importa del miembro para decidir. Un perfil borrado cuenta como inexistente.
/// <paramref name="MembershipOnHold"/>: su membresía vigente está en HoldByBilling (decisión de Nick,
/// 2026-10-17: entonces no se cobra la cuota). Lo calcula <see cref="AnnualFeeMemberQueries.FactsAsync"/>.
/// </summary>
public sealed record AnnualFeeMemberFacts(
    bool Exists, MemberType MemberType, MemberAccountStatus Status, DateTime EnrollDate, bool KeepQualified,
    bool MembershipOnHold = false, bool MembershipCancelled = false)
{
    public static readonly AnnualFeeMemberFacts Missing =
        new(false, MemberType.Ambassador, MemberAccountStatus.Terminated, DateTime.MinValue, false);

    public static AnnualFeeMemberFacts Of(MemberProfile? profile, bool membershipOnHold = false, bool membershipCancelled = false) =>
        profile is null || profile.IsDeleted
            ? Missing
            : new(true, profile.MemberType, profile.Status, profile.EnrollDate, profile.KeepQualified,
                  membershipOnHold, membershipCancelled);
}

public enum AnnualFeeDecisionKind
{
    /// <summary>Cobrar ahora.</summary>
    Charge,

    /// <summary>No cobrar nunca más: el estado pasa a Stopped.</summary>
    Stop,

    /// <summary>No cobrar hoy; volver a mirar mañana sin mover el vencimiento.</summary>
    Postpone,

    /// <summary>No cobrar; el vencimiento pasa a <see cref="AnnualFeeDecision.NextDue"/>.</summary>
    Reschedule
}

public sealed record AnnualFeeDecision(AnnualFeeDecisionKind Kind, string? Reason = null, DateTime? NextDue = null)
{
    public static readonly AnnualFeeDecision Charge = new(AnnualFeeDecisionKind.Charge);
}

/// <summary>
/// Las exclusiones de la cuota anual (Plan K, K5, K14). Puro: sin base ni reloj; lo usan Billing (para
/// cobrar) y el BizCenter (para decir si se puede pagar y por qué no).
/// </summary>
public static class AnnualFeeEligibility
{
    public const string ReasonAccountClosed  = "ACCOUNT_CLOSED";
    public const string ReasonProfileMissing = "PROFILE_MISSING";
    public const string ReasonNotAnAmbassador = "NOT_AN_AMBASSADOR";
    public const string ReasonUnderOneYear   = "UNDER_ONE_YEAR";
    public const string ReasonSuspended      = "ACCOUNT_SUSPENDED";
    public const string ReasonAccountPending = "ACCOUNT_PENDING";
    public const string ReasonMembershipOnHold = "MEMBERSHIP_ON_HOLD";
    public const string ReasonMembershipCancelled = "MEMBERSHIP_CANCELLED";
    public const string ReasonRecentlyBilled = "RECENTLY_BILLED";
    public const string ReasonKeepQualified  = "KEEP_QUALIFIED";
    public const string ReasonNotDue         = "NOT_DUE";

    /// <summary>
    /// ¿Se cobra hoy en automático? Orden del legado (BizCenterFeeMWRLife.BillMonthlyPayment):
    /// cancelada → menos de un año → suspendida → [membresía en HoldByBilling, Nick 2026-10-17] →
    /// cobrada hace poco → KeepQualified. Después, si no vence todavía, se reprograma (salvo forzado,
    /// que solo se salta esa última comprobación).
    /// </summary>
    public static AnnualFeeDecision ForAutomaticCharge(
        AnnualFeeMemberFacts member, DateTime nextBillingDate, DateTime? lastSuccessfulBilling, DateTime now, bool force)
    {
        var today = now.Date;

        // 0. Sin perfil (o borrado) o que ya no es Ambassador: no se cobra hoy y se vuelve a mirar
        //    mañana (revisión M4). Puede ser un dato que se corrige; parar sería para siempre.
        if (!member.Exists)
            return new(AnnualFeeDecisionKind.Postpone, ReasonProfileMissing);
        if (member.MemberType != MemberType.Ambassador)
            return new(AnnualFeeDecisionKind.Postpone, ReasonNotAnAmbassador);

        // 1. Cancelada: no se cobra nunca más. Terminada no se reactiva (decisiones de paridad).
        if (member.Status == MemberAccountStatus.Terminated)
            return new(AnnualFeeDecisionKind.Stop, ReasonAccountClosed);

        // 2. Menos de un año: el primer cobro es el aniversario.
        var firstAnniversary = AnnualFeeSchedule.FirstAnniversary(member.EnrollDate);
        if (today < firstAnniversary)
            return new(AnnualFeeDecisionKind.Reschedule, ReasonUnderOneYear,
                       Max(firstAnniversary, nextBillingDate.Date));

        // 3. Suspendida o pendiente de activar: se vuelve a mirar mañana (aquí no hay SuspendUntil).
        if (member.Status == MemberAccountStatus.Suspended)
            return new(AnnualFeeDecisionKind.Postpone, ReasonSuspended);
        if (member.Status == MemberAccountStatus.Pending)
            return new(AnnualFeeDecisionKind.Postpone, ReasonAccountPending);

        // 3b. Membresía en HoldByBilling (decisión de Nick, 2026-10-17): excluida de la cuota mientras
        //     siga así. Temporal como la suspensión: se mira mañana sin mover el vencimiento.
        if (member.MembershipOnHold)
            return new(AnnualFeeDecisionKind.Postpone, ReasonMembershipOnHold);

        // 3c. Membresía cancelada (decisión de Nick, M-8): no se cobra en automático hasta que reactive,
        //     sin mover el vencimiento. "Pagar ahora" sí sigue disponible (ForPayNow).
        if (member.MembershipCancelled)
            return new(AnnualFeeDecisionKind.Postpone, ReasonMembershipCancelled);

        // 4. Cobrada hace poco.
        if (lastSuccessfulBilling is { } last && last.Date > today.AddMonths(-AnnualFeeRules.RecentlyBilledMonths))
            return new(AnnualFeeDecisionKind.Reschedule, ReasonRecentlyBilled,
                       Max(nextBillingDate.Date, last.Date.AddYears(1)));

        // 5. KeepQualified: ni se cobra ni avanza (no cuenta como pagada; califica por la marca).
        if (member.KeepQualified)
            return new(AnnualFeeDecisionKind.Postpone, ReasonKeepQualified);

        // 6. Todavía no vence.
        if (!force && nextBillingDate.Date > today)
            return new(AnnualFeeDecisionKind.Reschedule, ReasonNotDue, nextBillingDate.Date);

        return AnnualFeeDecision.Charge;
    }

    /// <summary>
    /// ¿Puede el miembro pulsar "Pagar ahora"? Null si sí; si no, el código de AnnualFeeErrors. Lo que
    /// depende de pagos ya hechos (pagada, en curso, en revisión) lo mira quien tiene la base.
    /// </summary>
    public static string? ForPayNow(AnnualFeeMemberFacts member, DateTime nextBillingDate, DateTime now)
    {
        if (!member.Exists || member.MemberType != MemberType.Ambassador) return AnnualFeeErrors.NotApplicable;
        // Quien canceló la membresía (su cuenta queda Inactive) puede pagar para mantener su Biz Center
        // (decisión de Nick, M-8). Cualquier otra cuenta no activa, no.
        var cancelledButInactive = member.MembershipCancelled && member.Status == MemberAccountStatus.Inactive;
        if (member.Status != MemberAccountStatus.Active && !cancelledButInactive) return AnnualFeeErrors.AccountNotActive;
        if (member.MembershipOnHold)                                      return AnnualFeeErrors.MembershipOnHold;
        if (member.KeepQualified)                                         return AnnualFeeErrors.Waived;
        if (!AnnualFeeSchedule.IsPayable(nextBillingDate, now))           return AnnualFeeErrors.NotDue;
        return null;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
