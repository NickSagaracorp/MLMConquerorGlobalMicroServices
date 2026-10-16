namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <summary>
/// Historial de residuales diarios (DailyResidualEarnings) de un miembro. Lo comparten BizCenter
/// (con el miembro del token) y Admin (con el miembro de la ruta).
/// </summary>
public interface IDailyResidualReportService
{
    Task<DailyResidualHistoryView> GetHistoryAsync(
        string memberId, int? year, int? month, int page, int pageSize, CancellationToken ct = default);
}
