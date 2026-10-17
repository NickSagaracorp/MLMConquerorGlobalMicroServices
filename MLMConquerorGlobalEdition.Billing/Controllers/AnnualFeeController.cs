using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Controllers;

/// <summary>Cuota anual del Biz Center (Plan K). Lo llama el BizCenter reenviando el JWT del miembro.</summary>
/// <remarks>
/// QUIEN PAGA ES EL DEL TOKEN: el contrato no lleva MemberId. El personal (sin memberId) no paga por nadie;
/// una suplantación tampoco, sea o no de solo lectura. Todo pago de la cuota es con lo que ya es del
/// miembro (comisiones, eWallet o un token), así que exige siempre el permiso de step-up, validado aquí
/// también para que llamar directamente a Billing no se salte el código.
/// </remarks>
[ApiController]
[Route("api/v1/billing/annual-fee")]
[Authorize]
public class AnnualFeeController : ControllerBase
{
    private const string Treasury = AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.BillingManager;

    private readonly IAnnualFeeChargeService _service;
    private readonly IStepUpGrantCheck _stepUp;
    private readonly ILogger<AnnualFeeController> _logger;

    public AnnualFeeController(IAnnualFeeChargeService service, IStepUpGrantCheck stepUp, ILogger<AnnualFeeController> logger)
    {
        _service = service;
        _stepUp  = stepUp;
        _logger  = logger;
    }

    /// <summary>POST api/v1/billing/annual-fee/pay — "Pagar ahora" con token, comisiones o eWallet.</summary>
    [HttpPost("pay")]
    public async Task<IActionResult> Pay([FromBody] PayAnnualFeeRequest request, CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null)
            return Forbidden(AnnualFeeErrors.NotAMember, "Only members can pay their annual fee.");
        if (ImpersonationScope.IsImpersonating(User))
            return Forbidden(AnnualFeeErrors.Impersonation, "The annual fee cannot be paid during impersonation.");

        var userId  = CallerIdentity.UserIdOf(User) ?? string.Empty;
        var refusal = await AuthorizeAsync(memberId, userId, request.IdempotencyKey, ct);
        if (refusal is not null) return refusal;

        var actor  = string.IsNullOrEmpty(userId) ? memberId : userId;
        var result = await _service.PayNowAsync(new AnnualFeePayNowInput(memberId, actor, request), ct);
        if (result.IsSuccess) return Ok(ApiResponse<AnnualFeePaymentResultDto>.Ok(result.Value!));

        // Conflicto con lo guardado o con otra operación en curso: 409. Lo demás: la petición no vale (400).
        var body = ApiResponse<AnnualFeePaymentResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier);
        return result.ErrorCode is AnnualFeeErrors.IdempotencyKeyReused or AnnualFeeErrors.Busy
                                 or AnnualFeeErrors.InProgress or AnnualFeeErrors.AlreadyPaid
            ? StatusCode(StatusCodes.Status409Conflict, body)
            : BadRequest(body);
    }

    /// <summary>
    /// POST api/v1/billing/annual-fee/payments/{paymentId}/reconcile — tesorería cierra un pago a conciliar
    /// tras mirar i-Payout: cobrado (completa y avanza el ciclo) o no cobrado (fallido, el ciclo queda libre).
    /// </summary>
    [HttpPost("payments/{paymentId}/reconcile")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> Reconcile(string paymentId, [FromBody] ReconcileAnnualFeePaymentRequest request, CancellationToken ct)
    {
        var email  = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var actor  = string.IsNullOrEmpty(email) ? CallerIdentity.UserIdOf(User) ?? "admin" : email;
        var result = await _service.ReconcileAsync(paymentId, request, actor, ct);
        if (result.IsSuccess) return Ok(ApiResponse<AnnualFeePaymentResultDto>.Ok(result.Value!));

        var status = result.ErrorCode switch
        {
            AnnualFeeErrors.PaymentNotFound                         => StatusCodes.Status404NotFound,
            AnnualFeeErrors.NotUnderReview or AnnualFeeErrors.Busy => StatusCodes.Status409Conflict,
            _                                                       => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, ApiResponse<AnnualFeePaymentResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Mismo orden que la compra de tokens: (1) validar el permiso; sin él, 403 STEP_UP_REQUIRED. (2) Si la
    /// clave ya tiene pago, NO se quema: el servicio solo devolverá lo que ya pasó. (3) Si no, se quema
    /// con la marca de Billing antes de cobrar; ya usado, 403 STEP_UP_INVALID. Caché caída: falla cerrado.
    /// </summary>
    private async Task<IActionResult?> AuthorizeAsync(string memberId, string userId, string? idempotencyKey, CancellationToken ct)
    {
        var jti = _stepUp.ValidJti(SingleGrantHeader(), userId, StepUpOperations.AnnualFeeFromBalance);
        if (jti is null)
            return Forbidden(StepUpGrant.RequiredErrorCode, "Paying the annual fee requires a verification code.");

        if (await _service.HasPaymentAsync(memberId, idempotencyKey, ct))
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
            _logger.LogError(ex, "Pago de cuota rechazado: no se pudo marcar el permiso de step-up como usado. Usuario {UserId}, TraceId {TraceId}.",
                userId, HttpContext.TraceIdentifier);
            return Forbidden(StepUpGrant.InvalidErrorCode, "The verification could not be checked. Try again.");
        }

        if (!claimed)
        {
            _logger.LogWarning("Pago de cuota rechazado: permiso de step-up repetido. Usuario {UserId}, jti {Jti}, TraceId {TraceId}.",
                userId, jti, HttpContext.TraceIdentifier);
            return Forbidden(StepUpGrant.InvalidErrorCode, "This verification was already used. Request a new code.");
        }

        return null;
    }

    private string? SingleGrantHeader()
    {
        var values = Request.Headers[StepUpGrant.HeaderName];
        return values.Count == 1 ? values[0] : null;
    }

    private IActionResult Forbidden(string code, string message) =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(code, message, HttpContext.TraceIdentifier));
}
