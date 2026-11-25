namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;

/// <summary>
/// Contadores de la cabecera de la pantalla de comisiones, sobre TODA la tabla. Antes se contaban sobre la página
/// cargada ("Total (Page)"), que con el grid de servidor ya no existe en el cliente; van en su propia llamada para
/// que la lectura del grid no cargue con un agrupado de 2,4 millones de filas.
/// </summary>
public class CommissionStatsDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Paid { get; set; }
    public int Cancelled { get; set; }
}
