namespace MLMConquerorGlobalEdition.SharedComponents.Components.Team;

/// <summary>
/// Las mismas reglas que ReportDateRange del servidor, para avisar en la pantalla sin hacer la
/// petición. El servidor las vuelve a comprobar siempre; los límites (tope de días, primer día de
/// Actividad, hoy) llegan del servidor por <c>team/reports/setup</c>, nunca del reloj del navegador.
/// </summary>
public static class ReportRangeCheck
{
    /// <summary>Regla común (Cambios de estado): null si vale; si no, la clave de texto y su argumento.</summary>
    public static (string Key, object? Arg)? Problem(DateTime from, DateTime to, int maxDays)
    {
        if (to.Date < from.Date) return ("Reports.RangeInvalid", null);
        if ((to.Date - from.Date).TotalDays + 1 > maxDays) return ("Reports.RangeTooLong", maxDays);
        return null;
    }

    /// <summary>
    /// El texto que corresponde a un código de rango devuelto por el servidor en un 400 (la
    /// pantalla ya avisó antes, pero el servidor manda: su reloj puede haber cambiado de día).
    /// Cualquier otro código, o ninguno, es un error genérico.
    /// </summary>
    public static (string Key, object? Arg) FromServerCode(string? errorCode, DateTime minFrom, int maxDays) => errorCode switch
    {
        "REPORT_RANGE_INVALID"   => ("Reports.RangeInvalid", null),
        "REPORT_RANGE_TOO_OLD"   => ("Reports.RangeTooOld", minFrom.ToString("d")),
        "REPORT_RANGE_IN_FUTURE" => ("Reports.RangeInFuture", null),
        "REPORT_RANGE_TOO_LONG"  => ("Reports.RangeTooLong", maxDays),
        _                        => ("Common.Error", null)
    };

    /// <summary>
    /// Regla de Actividad por fechas (corrección de Nick N4): inicio no anterior a
    /// <paramref name="minFrom"/> (hoy − 3 meses) y fin no posterior a <paramref name="today"/>.
    /// </summary>
    public static (string Key, object? Arg)? ActivityProblem(DateTime from, DateTime to, DateTime minFrom, DateTime today)
    {
        if (to.Date < from.Date) return ("Reports.RangeInvalid", null);
        if (from.Date < minFrom.Date) return ("Reports.RangeTooOld", minFrom.ToString("d"));
        if (to.Date > today.Date) return ("Reports.RangeInFuture", null);
        return null;
    }
}
