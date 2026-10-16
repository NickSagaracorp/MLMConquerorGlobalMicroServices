namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Rango de fechas de los reportes del equipo. Los dos extremos son días inclusivos y se convierten
/// a [desde, hasta + 1 día).
///
/// Dos reglas (Plan J):
/// <list type="bullet">
///   <item><b>Común</b> (J12, la de <i>Cambios de estado</i>): como mucho <see cref="MaxDays"/> días.</item>
///   <item><b>Actividad por fechas</b> (corrección de Nick N4): además, el inicio no puede ser anterior
///   a hoy menos <see cref="ActivityLookbackMonths"/> meses y el fin no puede ser posterior a hoy.
///   «Hoy» es la hora del SERVIDOR, que pasa quien llama.</item>
/// </list>
/// </summary>
public readonly record struct ReportDateRange(DateTime Start, DateTime End)
{
    public const int    MaxDays                = 366;
    public const int    ActivityLookbackMonths = 3;

    public const string InvalidCode  = "REPORT_RANGE_INVALID";
    public const string TooLongCode  = "REPORT_RANGE_TOO_LONG";
    public const string TooOldCode   = "REPORT_RANGE_TOO_OLD";
    public const string InFutureCode = "REPORT_RANGE_IN_FUTURE";

    /// <summary>Regla común: fechas presentes, fin no anterior al inicio, como mucho <see cref="MaxDays"/> días.</summary>
    public static bool TryCreate(DateTime from, DateTime to, out ReportDateRange range, out string? errorCode)
    {
        range     = default;
        errorCode = null;

        if (from == default || to == default)
        {
            errorCode = InvalidCode;
            return false;
        }

        var start = from.Date;
        var end   = to.Date.AddDays(1);

        if (end <= start)
        {
            errorCode = InvalidCode;
            return false;
        }

        if ((end - start).TotalDays > MaxDays)
        {
            errorCode = TooLongCode;
            return false;
        }

        range = new ReportDateRange(start, end);
        return true;
    }

    /// <summary>
    /// Regla de <i>Actividad por fechas</i>: la común y, además, inicio no anterior a
    /// <see cref="ActivityMinFrom"/> (<see cref="TooOldCode"/>) y fin no posterior a hoy
    /// (<see cref="InFutureCode"/>).
    /// </summary>
    public static bool TryCreateForActivity(
        DateTime from, DateTime to, DateTime now, out ReportDateRange range, out string? errorCode)
    {
        if (!TryCreate(from, to, out range, out errorCode)) return false;

        if (from.Date < ActivityMinFrom(now))
        {
            range     = default;
            errorCode = TooOldCode;
            return false;
        }

        if (to.Date > now.Date)
        {
            range     = default;
            errorCode = InFutureCode;
            return false;
        }

        return true;
    }

    /// <summary>El primer día que admite <i>Actividad por fechas</i>: hoy menos tres meses.</summary>
    public static DateTime ActivityMinFrom(DateTime now) => now.Date.AddMonths(-ActivityLookbackMonths);

    /// <summary>Valores por defecto según la hora del SERVIDOR: del día 1 del mes en curso a hoy.</summary>
    public static ReportSetupView SetupFor(DateTime now) => new()
    {
        DefaultFrom     = new DateTime(now.Year, now.Month, 1),
        DefaultTo       = now.Date,
        MaxRangeDays    = MaxDays,
        ActivityMinFrom = ActivityMinFrom(now)
    };
}

/// <summary>Lo que necesitan las pantallas para pintar el filtro de fechas sin mirar el reloj del navegador.</summary>
public sealed class ReportSetupView
{
    public DateTime DefaultFrom     { get; set; }
    public DateTime DefaultTo       { get; set; }
    /// <summary>Tope de días de la regla común (Cambios de estado).</summary>
    public int      MaxRangeDays    { get; set; }
    /// <summary>Primer día admitido en Actividad por fechas (hoy − 3 meses).</summary>
    public DateTime ActivityMinFrom { get; set; }
}
