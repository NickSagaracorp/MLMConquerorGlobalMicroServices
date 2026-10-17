using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// La cuota anual del miembro que llama (Plan K). El miembro sale del token: no hay sujeto en la URL ni
/// en el cuerpo. El resumen y la preferencia se resuelven aquí; el pago lo hace Billing, al que se
/// reenvía el JWT y el permiso de step-up.
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/billing/annual-fee")]
[Authorize]
public class AnnualFeeController : ControllerBase
{
    private readonly IAnnualFeeOverviewService _overview;
    private readonly IBillingClient _billing;
    private readonly ICurrentUserService _currentUser;

    public AnnualFeeController(IAnnualFeeOverviewService overview, IBillingClient billing, ICurrentUserService currentUser)
    {
        _overview    = overview;
        _billing     = billing;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/v1/bizcenter/billing/annual-fee</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse<AnnualFeeOverviewDto>.Ok(await _overview.GetAsync(_currentUser.MemberId, ct)));

    /// <summary>
    /// PUT /api/v1/bizcenter/billing/annual-fee/preference — pagar las cuotas con el saldo (K7). Sin
    /// step-up (K15); nunca suplantando (la suplantación de solo lectura ya la corta el middleware).
    /// </summary>
    [HttpPut("preference")]
    public async Task<IActionResult> SetPreference([FromBody] AnnualFeePreferenceRequest request, CancellationToken ct)
    {
        if (ImpersonationScope.IsImpersonating(User))
            return ImpersonationForbidden();

        var actor = string.IsNullOrEmpty(_currentUser.UserId) ? _currentUser.MemberId : _currentUser.UserId;
        var saved = await _overview.SetFeesFromBalanceAsync(_currentUser.MemberId, request.FeesFromBalance, actor, ct);
        return saved
            ? Ok(ApiResponse<AnnualFeePreferenceRequest>.Ok(request))
            : BadRequest(ApiResponse<AnnualFeePreferenceRequest>.Fail(AnnualFeeErrors.NotApplicable,
                "Only Ambassadors have a Biz Center annual fee.", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// POST /api/v1/bizcenter/billing/annual-fee/pay — "Pagar ahora" con token, comisiones o eWallet.
    /// El filtro de step-up valida y quema el permiso antes de llegar aquí; se reenvía a Billing en
    /// X-Step-Up-Token, que lo valida y quema con su propia marca. Tarjeta: 400, hasta el Plan I.
    /// </summary>
    [HttpPost("pay")]
    [RequiresStepUp(StepUpOperations.AnnualFeeFromBalance)]
    public async Task<IActionResult> Pay([FromBody] PayAnnualFeeRequest request, CancellationToken ct)
    {
        if (ImpersonationScope.IsImpersonating(User))
            return ImpersonationForbidden();
        if (AnnualFeeMethods.Normalize(request.Method) is null)
            return BadRequest(ApiResponse<AnnualFeePaymentResultDto>.Fail(AnnualFeeErrors.MethodNotSupported,
                "This payment method is not available for the annual fee.", HttpContext.TraceIdentifier));

        var grant = Request.Headers[StepUpGrant.HeaderName].ToString();
        return this.ToActionResult(await _billing.PayAnnualFeeAsync(request, BearerToken.From(Request), grant, ct));
    }

    private IActionResult ImpersonationForbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
            AnnualFeeErrors.Impersonation, "The annual fee cannot be changed or paid during impersonation.", HttpContext.TraceIdentifier));
}
