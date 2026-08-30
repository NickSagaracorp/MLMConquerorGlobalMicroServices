using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.Charge;
using MLMConquerorGlobalEdition.Billing.Features.GetGateways;
using MLMConquerorGlobalEdition.Billing.Features.Payout;
using MLMConquerorGlobalEdition.Billing.Features.Refund;
using MLMConquerorGlobalEdition.Billing.Features.RenewMembership;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Controllers;

/// <summary>
/// La superficie HTTP de cobros, devoluciones, renovaciones y pagos.
/// </summary>
/// <remarks>
/// EL <c>[Authorize]</c> DE LA CLASE NO AUTORIZABA NADA. Comprobaba que hubiera sesión y ni una vez
/// DE QUIÉN era: el objetivo de cada operación venía del <c>MemberId</c> del CUERPO, y
/// <c>ICurrentUserService</c> solo se usaba para rellenar columnas de auditoría
/// (<c>CreatedBy</c>/<c>LastUpdateBy</c>), nunca para decidir. Con eso, cualquier cuenta autenticada
/// podía cobrar la tarjeta de otro miembro, renovarle la membresía o disparar el pago de sus
/// comisiones, con solo escribir su identificador en el JSON.
///
/// LO QUE SE APLICA AHORA, y por qué son dos reglas y no una:
///
///   • <c>charge</c> y <c>memberships/renew</c> tienen un sujeto claro —la cuenta a la que se le
///     cobra—, así que valen para autoservicio con la misma regla que el resto del sistema:
///     <see cref="CallerIdentity.CanActOnMember"/>, o es tu cuenta o eres personal.
///
///   • <c>refund</c> y <c>wallets/payout</c> NO tienen sujeto propio: una devolución identifica un
///     pago, no a un miembro, y un pago de comisiones mueve dinero de la casa. Son operaciones de
///     tesorería, y llevan los mismos tres roles con los que ya está cerrada la superficie
///     administrativa de facturación (<c>AdminBillingGatewayController</c>,
///     <c>AdminRecurringBillingController</c>). No es una lista inventada aquí: es la que el
///     sistema ya usa para esto.
///
/// ESTO ES UN CIERRE, NO UNA APERTURA. Hoy ningún cliente de la solución llama a estas cinco rutas
/// —el portal y el panel pasan por sus propias superficies—, así que lo único que cambia es quién
/// las alcanzaría desde fuera. Si mañana un servicio necesita llamarlas de máquina a máquina, lo que
/// hace falta es una identidad de servicio, no volver al <c>[Authorize]</c> pelado.
/// </remarks>
[ApiController]
[Route("api/v1/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    /// <summary>
    /// Quién puede mover dinero que no es de una sola cuenta. Misma lista que la superficie
    /// administrativa de facturación.
    /// </summary>
    private const string TesoreriaRoles =
        AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.BillingManager;

    private readonly IMediator _mediator;

    public BillingController(IMediator mediator)
        => _mediator = mediator;

    /// <summary>
    /// Charge a member via the specified payment gateway.
    /// Creates an Order and PaymentHistory record.
    /// For eWallet: also deducts from the member's internal balance.
    /// </summary>
    [HttpPost("charge")]
    [ProducesResponseType(typeof(ApiResponse<ChargeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest request, CancellationToken ct)
    {
        if (!User.CanActOnMember(request?.MemberId)) return Ajeno();

        var result = await _mediator.Send(new ChargeCommand(request!), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ChargeResponse>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ChargeResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Refund a previously captured payment.
    /// Supported gateways: Stripe (Dwolla) and eWallet.
    /// </summary>
    [HttpPost("refund")]
    [Authorize(Roles = TesoreriaRoles)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refund([FromBody] RefundRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefundCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<bool>.Ok(result.Value));
    }

    /// <summary>
    /// List all available payment gateways registered in the system.
    /// </summary>
    [HttpGet("gateways")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<GatewayInfoDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGateways(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGatewaysQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<GatewayInfoDto>>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<IEnumerable<GatewayInfoDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// Renew a member's membership subscription.
    /// Charges via the member's preferred wallet gateway.
    /// </summary>
    [HttpPost("memberships/renew")]
    [ProducesResponseType(typeof(ApiResponse<ChargeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RenewMembership([FromBody] MembershipRenewalRequest request, CancellationToken ct)
    {
        if (!User.CanActOnMember(request?.MemberId)) return Ajeno();

        var result = await _mediator.Send(new RenewMembershipCommand(request!), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ChargeResponse>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<ChargeResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Process commission payout for a member.
    /// Pays out all Pending CommissionEarnings where PaymentDate &lt;= today,
    /// crediting the member's eWallet internal balance.
    /// </summary>
    [HttpPost("wallets/payout")]
    [Authorize(Roles = TesoreriaRoles)]
    [ProducesResponseType(typeof(ApiResponse<PayoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Payout([FromBody] PayoutRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new PayoutCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PayoutResponse>.Fail(
                result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<PayoutResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// 403 y no 404: quien llama está autenticado y la ruta existe, lo que no tiene es permiso sobre
    /// esa cuenta.
    /// </summary>
    private IActionResult Ajeno() =>
        StatusCode(StatusCodes.Status403Forbidden,
            ApiResponse<bool>.Fail("FORBIDDEN", "Esa cuenta no es la tuya.",
                HttpContext.TraceIdentifier));
}
