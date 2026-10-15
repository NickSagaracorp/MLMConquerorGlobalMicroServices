using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetGuestPasses;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenBalances;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenTransactions;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/tokens")]
[Authorize]
public class TokensController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IBillingClient _billing;

    public TokensController(IMediator mediator, IBillingClient billing)
    {
        _mediator = mediator;
        _billing  = billing;
    }

    /// <summary>GET /api/v1/bizcenter/tokens — paged token transaction history for current member</summary>
    [HttpGet]
    public async Task<IActionResult> GetTokenTransactions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTokenTransactionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<TokenTransactionDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<TokenTransactionDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/tokens/balances — current token balances per type for current member</summary>
    [HttpGet("balances")]
    public async Task<IActionResult> GetTokenBalances(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTokenBalancesQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<TokenBalanceDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<TokenBalanceDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/tokens/guest-passes — guest pass token balances only</summary>
    [HttpGet("guest-passes")]
    public async Task<IActionResult> GetGuestPasses(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGuestPassesQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<TokenBalanceDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<TokenBalanceDto>>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/bizcenter/tokens/distribute — distribute tokens to a downline member</summary>
    [HttpPost("distribute")]
    public async Task<IActionResult> DistributeToken([FromBody] DistributeTokenRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new DistributeTokenCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Tokens distributed successfully."));
    }

    /// <summary>
    /// POST /api/v1/bizcenter/tokens/instances/transfer — transfer one specific token (by code)
    /// to a recipient in the caller's enrollment subtree, preserving chain-of-custody.
    /// </summary>
    [HttpPost("instances/transfer")]
    public async Task<IActionResult> TransferInstance(
        [FromBody] TransferTokenInstanceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new TransferTokenInstanceCommand(request.TokenCode, request.RecipientMemberId, request.Notes), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<TransferTokenInstanceResponse>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<TransferTokenInstanceResponse>.Ok(result.Value!, "Token transferred successfully."));
    }

    // ─── Compra de tokens (Plan F) ──────────────────────────────────────────
    // El dinero lo resuelve Billing con el JWT del miembro: aquí no se calcula precio, cupo ni saldo,
    // y la petición no lleva MemberId. El estado y el código de Billing llegan tal cual a la pantalla.

    /// <summary>GET /api/v1/bizcenter/tokens/summary — total, usados y disponibles del miembro.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTokenSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<TokenSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<TokenSummaryDto>.Ok(result.Value!));
    }

    /// <summary>
    /// GET /api/v1/bizcenter/tokens/types — catálogo de compra (precio, si se vende, cupo, monedas
    /// cripto y si se acepta tarjeta). Lo calcula Billing.
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetPurchasableTypes(CancellationToken ct) =>
        this.ToActionResult(await _billing.GetTokenCatalogAsync(BearerToken.From(Request), ct));

    /// <summary>
    /// POST /api/v1/bizcenter/tokens/purchase — compra con tarjeta o cripto. Pagar con saldo por aquí es
    /// 400 PAYMENT_METHOD_WRONG_ENDPOINT: tiene su propia ruta, con step-up. No reembolsable.
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseTokensRequest request, CancellationToken ct)
    {
        if (ImpersonationScope.IsImpersonating(User))
            return ImpersonationForbidden();
        if (TokenPaymentMethods.IsBalance(request.PaymentMethod))
            return WrongEndpoint("Paying with your balance uses tokens/purchase/balance.");

        // Sin permiso aunque venga uno en la cabecera: esta ruta no lo ha validado ni quemado.
        return this.ToActionResult(await _billing.PurchaseTokensAsync(request, BearerToken.From(Request), null, ct));
    }

    /// <summary>
    /// POST /api/v1/bizcenter/tokens/purchase/balance — compra con comisiones o eWallet. Exige un permiso
    /// de step-up (lo valida y quema el filtro antes de llegar aquí); se reenvía a Billing en
    /// X-Step-Up-Token, que lo valida y lo quema con su propia marca. Un reintento tras un 502 necesita
    /// un permiso nuevo y la misma clave de idempotencia.
    /// </summary>
    [HttpPost("purchase/balance")]
    [RequiresStepUp(StepUpOperations.TokenPurchaseFromBalance)]
    public async Task<IActionResult> PurchaseFromBalance([FromBody] PurchaseTokensRequest request, CancellationToken ct)
    {
        if (ImpersonationScope.IsImpersonating(User))
            return ImpersonationForbidden();
        if (!TokenPaymentMethods.IsBalance(request.PaymentMethod))
            return WrongEndpoint("Card and crypto purchases use tokens/purchase.");

        var grant = Request.Headers[StepUpGrant.HeaderName].ToString();
        return this.ToActionResult(await _billing.PurchaseTokensAsync(request, BearerToken.From(Request), grant, ct));
    }

    /// <summary>
    /// POST /api/v1/bizcenter/tokens/purchases/{purchaseId}/cancel — el miembro retira una compra en
    /// cripto que no va a pagar y recupera el cupo. Billing comprueba que es suya y que sigue sin pagar.
    /// El id de TokenPurchase es un Guid en texto: cualquier otra cosa es 404 aquí, sin llamar a Billing.
    /// </summary>
    [HttpPost("purchases/{purchaseId:guid}/cancel")]
    public async Task<IActionResult> CancelPurchase(string purchaseId, CancellationToken ct)
    {
        if (ImpersonationScope.IsImpersonating(User))
            return ImpersonationForbidden();

        return this.ToActionResult(await _billing.CancelTokenPurchaseAsync(purchaseId, BearerToken.From(Request), ct));
    }

    private IActionResult WrongEndpoint(string message) =>
        BadRequest(ApiResponse<TokenPurchaseResultDto>.Fail(TokenPurchaseErrors.WrongEndpoint, message, HttpContext.TraceIdentifier));

    // Billing también lo rechaza; aquí se corta antes para no llamar en vano. En la ruta de saldo el
    // filtro de step-up ya quemó el permiso del BizCenter: bajo suplantación se pierde, y es correcto,
    // porque ese permiso no debía poder usarse para comprar.
    private IActionResult ImpersonationForbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<TokenPurchaseResultDto>.Fail(
            TokenPurchaseErrors.Impersonation, "Purchases are not allowed during impersonation.", HttpContext.TraceIdentifier));
}

/// <summary>Request body for instance-level token transfer.</summary>
public class TransferTokenInstanceRequest
{
    public string TokenCode { get; set; } = string.Empty;
    public string RecipientMemberId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
