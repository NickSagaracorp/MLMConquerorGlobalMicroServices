using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Loyalty;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Puntos de lealtad del miembro de la ruta, para el perfil de miembro del Admin. Espejo de
/// <c>LoyaltyController</c> del BizCenter: mismo servicio y misma respuesta; el miembro sale de la ruta
/// y no del token. Solo lectura: el desbloqueo manual es <c>AdminLoyaltyController</c>.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/loyalty")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberLoyaltyController : ControllerBase
{
    private readonly ILoyaltyPointsService _loyalty;

    public AdminMemberLoyaltyController(ILoyaltyPointsService loyalty) => _loyalty = loyalty;

    /// <summary>GET .../loyalty?page=1&amp;pageSize=20</summary>
    [HttpGet]
    public async Task<IActionResult> Get(string memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<LoyaltyPointsPageView>.Ok(await _loyalty.GetPageAsync(memberId, page, pageSize, ct)));
}
