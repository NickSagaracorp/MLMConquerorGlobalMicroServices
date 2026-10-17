using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// La cuota anual de un miembro en su perfil del Admin (Plan K, K21). Solo lectura: pagar y cambiar la
/// preferencia son cosas del miembro. Mismos roles que AdminMemberProfileController.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/billing/annual-fee")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager,SupportLevel1,SupportLevel2,SupportLevel3")]
public class AdminMemberAnnualFeeController : ControllerBase
{
    private readonly IAnnualFeeOverviewService _overview;

    public AdminMemberAnnualFeeController(IAnnualFeeOverviewService overview) => _overview = overview;

    /// <summary>GET /api/v1/admin/members/{memberId}/billing/annual-fee</summary>
    [HttpGet]
    public async Task<IActionResult> Get(string memberId, CancellationToken ct) =>
        Ok(ApiResponse<AnnualFeeOverviewDto>.Ok(await _overview.GetAsync(memberId, ct)));
}
