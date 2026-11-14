using System.Globalization;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Un período de la gráfica: días completos e inclusivos [Start, End] y su etiqueta.</summary>
public readonly record struct ReportPeriod(DateTime Start, DateTime End, string Label);

/// <summary>
/// Períodos de las vistas de Top Countries. Las semanas empiezan el lunes y se cortan en los límites del mes, para que
/// cada semana pertenezca a un solo mes (decisión del 2026-11-14). Etiquetas en cultura invariante.
/// </summary>
public static class ReportPeriods
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Los meses completos desde el mes de <paramref name="from"/> hasta el de <paramref name="to"/>.</summary>
    public static IReadOnlyList<ReportPeriod> Months(DateTime from, DateTime to)
    {
        var list = new List<ReportPeriod>();
        for (var m = new DateTime(from.Year, from.Month, 1); m <= to.Date; m = m.AddMonths(1))
            list.Add(new ReportPeriod(m, m.AddMonths(1).AddDays(-1), m.ToString("MMM yyyy", Inv)));
        return list;
    }

    /// <summary>La semana (lunes a domingo, cortada por el mes) que contiene <paramref name="date"/>.</summary>
    public static ReportPeriod WeekOf(DateTime date)
    {
        var day    = date.Date;
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var first  = new DateTime(day.Year, day.Month, 1);
        var last   = first.AddMonths(1).AddDays(-1);
        var start  = monday < first ? first : monday;
        var end    = monday.AddDays(6) > last ? last : monday.AddDays(6);
        return new ReportPeriod(start, end, Label(start, end));
    }

    /// <summary>Las semanas del mes, de la que contiene el día 1 a la que contiene el último.</summary>
    public static IReadOnlyList<ReportPeriod> WeeksOfMonth(int year, int month)
    {
        var list = new List<ReportPeriod>();
        var last = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        for (var d = new DateTime(year, month, 1); d <= last;)
        {
            var week = WeekOf(d);
            list.Add(week);
            d = week.End.AddDays(1);
        }
        return list;
    }

    /// <summary>Los días de la semana (ya cortada por el mes) que contiene <paramref name="date"/>.</summary>
    public static IReadOnlyList<ReportPeriod> DaysOfWeek(DateTime date)
    {
        var week = WeekOf(date);
        var list = new List<ReportPeriod>();
        for (var d = week.Start; d <= week.End; d = d.AddDays(1))
            list.Add(new ReportPeriod(d, d, d.ToString("ddd MMM d", Inv)));
        return list;
    }

    private static string Label(DateTime start, DateTime end) =>
        start == end ? start.ToString("MMM d", Inv) : $"{start.ToString("MMM d", Inv)}-{end.Day}";
}
