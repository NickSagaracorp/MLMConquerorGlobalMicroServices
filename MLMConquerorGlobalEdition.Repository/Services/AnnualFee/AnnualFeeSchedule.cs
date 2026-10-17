using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// El calendario de la cuota anual (Plan K). Puro: sin base ni reloj. Todas las fechas son de servidor
/// y se trabajan sin hora (.Date).
/// </summary>
public static class AnnualFeeSchedule
{
    /// <summary>Inscripción + 1 año: el ancla de Nick.</summary>
    public static DateTime FirstAnniversary(DateTime enrollDate) => enrollDate.Date.AddYears(1);

    /// <summary>
    /// Primer vencimiento al crear el estado (K3): el primer aniversario de la inscripción que no haya
    /// pasado ya (hoy incluido). No se cobra hacia atrás: el estado nace el día que se despliega, no el
    /// día que se inscribió el miembro. AddYears desde la inscripción cada vez, para que un 29 de febrero
    /// caiga el 28 en los años no bisiestos sin arrastrar el 28 a los bisiestos.
    /// </summary>
    public static DateTime FirstDueOnCreation(DateTime enrollDate, DateTime now)
    {
        var today = now.Date;
        for (var years = 1; ; years++)
        {
            var due = enrollDate.Date.AddYears(years);
            if (due >= today) return due;
        }
    }

    /// <summary>
    /// El siguiente vencimiento después de pagar el ciclo <paramref name="cycleDue"/> (K4, revisado por
    /// Nick el 2026-10-17): pagado el día del vencimiento o antes, un año después del vencimiento (pagar
    /// antes no hace perder días); pagado después, un año desde el pago (sin gracia que conserve el aniversario).
    /// </summary>
    public static DateTime NextDueAfterPayment(DateTime cycleDue, DateTime paidAt)
    {
        var due  = cycleDue.Date;
        var paid = paidAt.Date;
        return paid <= due ? due.AddYears(1) : paid.AddYears(1);
    }

    /// <summary>¿Se puede pagar ya? Desde <see cref="AnnualFeeRules.PayAheadDays"/> días antes del vencimiento.</summary>
    public static bool IsPayable(DateTime nextDue, DateTime now) =>
        nextDue.Date <= now.Date.AddDays(AnnualFeeRules.PayAheadDays);
}
