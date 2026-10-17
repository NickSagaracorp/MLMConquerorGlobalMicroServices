namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <summary>Una fila de DailyResidualEarnings tal como la ve el miembro.</summary>
public sealed class DailyResidualRowView
{
    public long      Id             { get; set; }
    public DateTime  EarnedDate     { get; set; }
    public decimal   Amount         { get; set; }
    /// <summary>"Paid" o "Pending" (los "Cancelled" no salen).</summary>
    public string    Status         { get; set; } = string.Empty;
    public DateTime? PaymentDate    { get; set; }
    /// <summary>Puntos del eje que calificó (DT o ET; el otro es 0). Null en filas migradas sin dato.</summary>
    public int?      EligiblePoints { get; set; }
    public string?   PaymentComment { get; set; }
}

/// <summary>Una página del historial de un periodo (un mes o un año) y sus totales.</summary>
public sealed class DailyResidualHistoryView
{
    public List<DailyResidualRowView> Items { get; set; } = new();
    public int       TotalCount     { get; set; }
    public int       Page           { get; set; }
    public int       PageSize       { get; set; }
    /// <summary>Año del periodo que se devolvió (el pedido o, si no vale, el del reloj del servidor).</summary>
    public int       Year           { get; set; }
    /// <summary>Mes del periodo; null = el año entero.</summary>
    public int?      Month          { get; set; }
    public decimal   PeriodTotal    { get; set; }
    public decimal   PeriodPaid     { get; set; }
    public decimal   PeriodPending  { get; set; }
    /// <summary>Años que se pueden elegir, del más reciente al más antiguo.</summary>
    public List<int> AvailableYears { get; set; } = new();
}
