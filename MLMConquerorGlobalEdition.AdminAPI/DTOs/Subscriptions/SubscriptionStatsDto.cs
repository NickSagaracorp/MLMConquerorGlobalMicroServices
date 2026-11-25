namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Subscriptions;

/// <summary>
/// Contadores de la cabecera de Subscriptions, calculados sobre TODA la tabla. Antes la pagina los contaba sobre las
/// filas de la pagina cargada, asi que nunca pasaban del tamano de pagina.
/// </summary>
public class SubscriptionStatsDto
{
    public int Active { get; set; }
    public int ExpiringIn7Days { get; set; }
    public int Cancelled { get; set; }
    public int AutoRenew { get; set; }
}
