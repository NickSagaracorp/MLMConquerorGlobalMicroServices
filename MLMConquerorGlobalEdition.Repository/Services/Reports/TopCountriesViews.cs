namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Vista de la gráfica de Top Countries (mensual por defecto).</summary>
public enum CountryChartFrequency
{
    Monthly,
    Weekly,
    Daily
}

/// <summary>Una fila de la tabla de países.</summary>
public sealed class CountryRowView
{
    public string Country     { get; init; } = string.Empty;
    public int    Total       { get; init; }
    public int    Ambassadors { get; init; }
    public int    Customers   { get; init; }

    /// <summary>Total − Total de 30 días antes; null si no hay foto de entonces.</summary>
    public int?   Change      { get; init; }
}

/// <summary>La tabla de Top Countries: la foto usada, contra cuál se compara y las filas.</summary>
public sealed class TopCountriesTableView
{
    public DateTime?                     SnapshotDate { get; init; }
    public DateTime?                     ComparedWith { get; init; }
    public IReadOnlyList<CountryRowView> Rows         { get; init; } = Array.Empty<CountryRowView>();
}

/// <summary>Una barra de la gráfica: el total de la última foto del período (null si no hay foto en él).</summary>
public sealed class CountryChartPointView
{
    public string    Label        { get; init; } = string.Empty;
    public DateTime  Start        { get; init; }
    public DateTime  End          { get; init; }
    public DateTime? SnapshotDate { get; init; }
    public int?      Total        { get; init; }
}
