using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Controllers;

/// <summary>Compra de tokens. Lo llama el BizCenter reenviando el JWT del miembro.</summary>
/// <remarks>
/// EL COMPRADOR ES EL DEL TOKEN. El contrato no lleva MemberId. El personal (sin memberId) no compra
/// por nadie; una suplantación tampoco, sea o no de solo lectura. Pagar con saldo exige el permiso de
/// step-up (el BizCenter ya lo quemó; aquí solo se valida) para que llamar directamente a Billing no
/// se salte el código.
/// </remarks>
[ApiController]
[Route("api/v1/billing/tokens")]
[Authorize]
public class TokenPurchasesController : ControllerBase
{
    private const string Treasury = AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.BillingManager;

    private readonly ITokenPurchaseService _service;
    private readonly IStepUpGrantCheck _stepUp;

    public TokenPurchasesController(ITokenPurchaseService service, IStepUpGrantCheck stepUp)
    {
        _service = service;
        _stepUp  = stepUp;
    }

    /// <summary>GET api/v1/billing/tokens/catalog — tipos con precio, cupo del mes y monedas cripto disponibles.</summary>
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null) return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can buy tokens.");
        return Ok(ApiResponse<TokenCatalogDto>.Ok(await _service.GetCatalogAsync(memberId, ct)));
    }

    /// <summary>GET api/v1/billing/tokens/funds — comisiones gastables y saldo real de eWallet.</summary>
    [HttpGet("funds")]
    public async Task<IActionResult> Funds(CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null) return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can buy tokens.");
        return Ok(ApiResponse<TokenPurchaseFundsDto>.Ok(await _service.GetFundsAsync(memberId, ct)));
    }

    /// <summary>POST api/v1/billing/tokens/purchase — compra. No reembolsable.</summary>
    /// <remarks>
    /// Una sola ruta para los cuatro métodos: el BizCenter las separa (la de saldo quema el permiso) y
    /// aquí lo que importa es que un método de saldo nunca pase sin un permiso válido.
    /// </remarks>
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseTokensRequest request, CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null)
            return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can buy tokens.");
        if (ImpersonationScope.IsImpersonating(User))
            return Forbidden(TokenPurchaseErrors.Impersonation, "Purchases are not allowed during impersonation.");

        var userId = CallerIdentity.UserIdOf(User) ?? string.Empty;
        if (TokenPaymentMethods.IsBalance(request.PaymentMethod)
            && !_stepUp.IsValid(SingleGrantHeader(), userId, StepUpOperations.TokenPurchaseFromBalance))
            return Forbidden(StepUpGrant.RequiredErrorCode, "Paying with your balance requires a verification code.");

        var actor  = string.IsNullOrEmpty(userId) ? memberId : userId;
        var result = await _service.PurchaseAsync(new TokenPurchaseInput(memberId, actor, request), ct);

        if (result.IsSuccess) return Ok(ApiResponse<TokenPurchaseResultDto>.Ok(result.Value!));

        // La clave ya se usó para otra compra: es un conflicto con lo guardado, no una petición mal
        // formada, y la pantalla debe empezar con una clave nueva.
        var body = ApiResponse<TokenPurchaseResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier);
        return result.ErrorCode == TokenPurchaseErrors.IdempotencyKeyReused
            ? StatusCode(StatusCodes.Status409Conflict, body)
            : BadRequest(body);
    }

    /// <summary>POST api/v1/billing/tokens/purchases/crypto/{orderId}/confirm — un administrador confirma la transferencia.</summary>
    [HttpPost("purchases/crypto/{orderId}/confirm")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> ConfirmCrypto(
        string orderId, [FromBody] ConfirmCryptoTokenPurchaseRequest request, CancellationToken ct)
    {
        var result = await _service.ConfirmCryptoAsync(orderId, request,
            CallerIdentity.UserIdOf(User) ?? string.Empty, EmailOf(User), ct);
        return FromAdminResult(result);
    }

    /// <summary>
    /// POST api/v1/billing/tokens/purchases/{purchaseId}/reconcile — tesorería cierra una compra a
    /// conciliar tras mirar i-Payout: cobrada (emite) o no cobrada (libera el cupo).
    /// </summary>
    [HttpPost("purchases/{purchaseId}/reconcile")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> Reconcile(
        string purchaseId, [FromBody] ReconcileTokenPurchaseRequest request, CancellationToken ct)
    {
        var actor = EmailOf(User);
        var result = await _service.ReconcileAsync(purchaseId, request,
            string.IsNullOrEmpty(actor) ? CallerIdentity.UserIdOf(User) ?? "admin" : actor, ct);
        return FromAdminResult(result);
    }

    /// <summary>
    /// POST api/v1/billing/tokens/purchases/{purchaseId}/cancel — el miembro retira su compra en cripto
    /// antes de pagarla y libera el cupo (D18). Solo la suya; nunca suplantando.
    /// </summary>
    [HttpPost("purchases/{purchaseId}/cancel")]
    public async Task<IActionResult> Cancel(string purchaseId, CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null)
            return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can cancel their purchases.");
        if (ImpersonationScope.IsImpersonating(User))
            return Forbidden(TokenPurchaseErrors.Impersonation, "Purchases cannot be changed during impersonation.");

        var userId = CallerIdentity.UserIdOf(User);
        var result = await _service.CancelCryptoAsync(memberId, purchaseId,
            string.IsNullOrEmpty(userId) ? memberId : userId, ct);
        return FromAdminResult(result);
    }

    /// <summary>
    /// POST api/v1/billing/tokens/purchases/crypto/{orderId}/reject — un aprobador de cripto rechaza la
    /// compra (la transferencia no llegó o no vale). Libera el cupo; no emite (D18).
    /// </summary>
    [HttpPost("purchases/crypto/{orderId}/reject")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> RejectCrypto(
        string orderId, [FromBody] RejectCryptoTokenPurchaseRequest request, CancellationToken ct)
    {
        var result = await _service.RejectCryptoAsync(orderId, request,
            CallerIdentity.UserIdOf(User) ?? string.Empty, EmailOf(User), ct);
        return FromAdminResult(result);
    }

    /// <summary>
    /// El permiso solo si viene una vez. Dos cabeceras no son "una de las dos": no hay forma correcta de
    /// elegir, igual que en StepUpAuthorizationFilter.
    /// </summary>
    private string? SingleGrantHeader()
    {
        var values = Request.Headers[StepUpGrant.HeaderName];
        return values.Count == 1 ? values[0] : null;
    }

    private IActionResult FromAdminResult(Result<TokenPurchaseResultDto> result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<TokenPurchaseResultDto>.Ok(result.Value!));

        var status = result.ErrorCode switch
        {
            TokenPurchaseErrors.CryptoPaymentNotFound or TokenPurchaseErrors.PurchaseNotFound => StatusCodes.Status404NotFound,
            TokenPurchaseErrors.CryptoAlreadyConfirmed or TokenPurchaseErrors.NotUnderReview
                or TokenPurchaseErrors.NotAwaitingCrypto or TokenPurchaseErrors.IssueFailed => StatusCodes.Status409Conflict,
            _                                                                                 => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, ApiResponse<TokenPurchaseResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    private static string EmailOf(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email") ?? string.Empty;

    private IActionResult Forbidden(string code, string message) =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.Fail(code, message, HttpContext.TraceIdentifier));
}
