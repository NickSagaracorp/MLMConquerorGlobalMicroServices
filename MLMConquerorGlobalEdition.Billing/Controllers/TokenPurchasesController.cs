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
/// step-up para que llamar directamente a Billing no se salte el código; ver AuthorizeBalanceAsync.
///
/// Los errores van como ApiResponse&lt;object&gt; ("data":null) o con el tipo de la respuesta, nunca con
/// "data":false: el BizCenter lee el sobre y un dato de otro tipo no debe estorbar al código.
/// </remarks>
[ApiController]
[Route("api/v1/billing/tokens")]
[Authorize]
public class TokenPurchasesController : ControllerBase
{
    private const string Treasury = AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.BillingManager;

    private readonly ITokenPurchaseService _service;
    private readonly IStepUpGrantCheck _stepUp;
    private readonly ILogger<TokenPurchasesController> _logger;

    public TokenPurchasesController(
        ITokenPurchaseService service, IStepUpGrantCheck stepUp, ILogger<TokenPurchasesController> logger)
    {
        _service = service;
        _stepUp  = stepUp;
        _logger  = logger;
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
        if (TokenPaymentMethods.IsBalance(request.PaymentMethod))
        {
            var refusal = await AuthorizeBalanceAsync(memberId, userId, request.IdempotencyKey, ct);
            if (refusal is not null) return refusal;
        }

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
    /// Pagar con saldo: null si puede seguir, o el 403 con que se rechaza.
    /// </summary>
    /// <remarks>
    /// ORDEN: (1) validar el permiso (firma, vigencia, usuario, operación); sin él, 403 STEP_UP_REQUIRED y
    /// no se toca la caché. (2) Si la clave de idempotencia ya tiene compra de este miembro, NO se quema:
    /// el servicio solo devolverá lo que ya pasó (o IDEMPOTENCY_KEY_REUSED) y no mueve dinero, así que una
    /// repetición pasa con un permiso válido aunque ya esté quemado. (3) Si no, se quema con la marca de
    /// Billing ANTES de cobrar; ya usado, 403 STEP_UP_INVALID. Con eso el mismo permiso no vale para una
    /// segunda compra (clave nueva) llamando a Billing directamente.
    ///
    /// Dos peticiones simultáneas con el mismo permiso y la misma clave nueva: una quema y compra; la otra
    /// choca con la marca (403) o, si llega cuando la compra ya está guardada, recibe la repetición. Nunca
    /// cobran las dos.
    ///
    /// FALLA CERRADO: si la caché no responde, 403 STEP_UP_INVALID, igual que StepUpAuthorizationFilter.
    /// </remarks>
    private async Task<IActionResult?> AuthorizeBalanceAsync(
        string memberId, string userId, string? idempotencyKey, CancellationToken ct)
    {
        var jti = _stepUp.ValidJti(SingleGrantHeader(), userId, StepUpOperations.TokenPurchaseFromBalance);
        if (jti is null)
            return Forbidden(StepUpGrant.RequiredErrorCode, "Paying with your balance requires a verification code.");

        if (await _service.HasPurchaseAsync(memberId, idempotencyKey, ct))
            return null;

        bool claimed;
        try
        {
            claimed = await _stepUp.TryConsumeAsync(jti, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Compra con saldo rechazada: no se pudo marcar el permiso de step-up como usado. Usuario {UserId}, TraceId {TraceId}.",
                userId, HttpContext.TraceIdentifier);
            return Forbidden(StepUpGrant.InvalidErrorCode, "The verification could not be checked. Try again.");
        }

        if (!claimed)
        {
            _logger.LogWarning(
                "Compra con saldo rechazada: permiso de step-up repetido. Usuario {UserId}, jti {Jti}, TraceId {TraceId}.",
                userId, jti, HttpContext.TraceIdentifier);
            return Forbidden(StepUpGrant.InvalidErrorCode, "This verification was already used. Request a new code.");
        }

        return null;
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
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(code, message, HttpContext.TraceIdentifier));
}
