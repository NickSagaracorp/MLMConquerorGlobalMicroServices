using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Dashboard;
using MLMConquerorGlobalEdition.Repository.Services.Loyalty;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Portada y puntos de lealtad del miembro de la ruta, para el perfil de miembro del Admin. Espejo
/// de <c>DashboardController</c> y <c>LoyaltyController</c> del BizCenter: mismos servicios, mismas
/// respuestas; el miembro sale de la ruta y no del token. Solo lectura: el desbloqueo manual de
/// lealtad ya existe en <c>AdminLoyaltyController</c>.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberDashboardController : ControllerBase
{
    private readonly IMemberDashboardService _dashboard;
    private readonly IMemberLoyaltyService   _loyalty;

    public AdminMemberDashboardController(IMemberDashboardService dashboard, IMemberLoyaltyService loyalty)
    {
        _dashboard = dashboard;
        _loyalty   = loyalty;
    }

    /// <summary>GET .../dashboard/stats — cifras de la portada del miembro.</summary>
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetStats(string memberId, CancellationToken ct = default) =>
        Ok(ApiResponse<MemberDashboardView>.Ok(await _dashboard.GetStatsAsync(memberId, ct)));

    /// <summary>GET .../loyalty?page=&amp;pageSize= — puntos de lealtad por páginas y totales.</summary>
    [HttpGet("loyalty")]
    public async Task<IActionResult> GetLoyalty(
        string memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<LoyaltyPointsPageView>.Ok(await _loyalty.GetPageAsync(memberId, page, pageSize, ct)));
}
