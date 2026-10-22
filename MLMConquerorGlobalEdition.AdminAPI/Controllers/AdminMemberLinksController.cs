using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Links;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// "Mis enlaces" del miembro de la ruta, para el perfil de miembro del Admin. Espejo de
/// <c>MyLinksController</c> del BizCenter: mismo servicio y mismas plantillas; el miembro sale de
/// la ruta y no del token.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/my-links")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberLinksController : ControllerBase
{
    private readonly IMyLinksService _links;

    public AdminMemberLinksController(IMyLinksService links) => _links = links;

    /// <summary>GET .../my-links</summary>
    [HttpGet]
    public async Task<IActionResult> Get(string memberId, CancellationToken ct = default) =>
        Ok(ApiResponse<MyLinksView>.Ok(await _links.GetAsync(memberId, ct)));
}
