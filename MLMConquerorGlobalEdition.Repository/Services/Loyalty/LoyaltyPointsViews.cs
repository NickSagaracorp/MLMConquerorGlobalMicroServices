using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Loyalty;

/// <summary>Un registro de puntos de lealtad del miembro (un mes, un producto, un pedido).</summary>
public class LoyaltyPointView
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal PointsEarned { get; set; }
    public bool IsLocked { get; set; }
    public bool MissedPayment { get; set; }
    public int NumberOfSuccessPayments { get; set; }
    public int MonthNo { get; set; }
    public int YearNo { get; set; }
    public DateTime? UnlockedAt { get; set; }
}

/// <summary>
/// Pagina de puntos de lealtad mas los totales de TODOS los registros del miembro (no solo los de la pagina):
/// total = suma de lo ganado, desbloqueados = los que ya no estan bloqueados, bloqueados = los que lo siguen.
/// </summary>
public class LoyaltyPointsPageView : PagedResult<LoyaltyPointView>
{
    public decimal TotalPoints { get; set; }
    public decimal UnlockedPoints { get; set; }
    public decimal LockedPoints { get; set; }
}
