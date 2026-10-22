namespace MLMConquerorGlobalEdition.Repository.Services.Loyalty;

/// <summary>
/// Puntos de lealtad de un miembro. Lo usan el BizCenter (miembro del token) y el Admin (miembro de la ruta):
/// quien llama pasa el miembro.
/// </summary>
public interface ILoyaltyPointsService
{
    /// <summary>Registros del miembro, del mes mas reciente al mas antiguo, con los totales de todos sus registros.</summary>
    Task<LoyaltyPointsPageView> GetPageAsync(string memberId, int page, int pageSize, CancellationToken ct = default);
}
