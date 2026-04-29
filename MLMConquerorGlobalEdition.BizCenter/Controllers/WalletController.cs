using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.AddCreditCard;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.DeleteCreditCard;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetBillingHistory;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetCreditCards;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.ReorderCreditCards;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.SetDefaultCreditCard;
using MLMConquerorGlobalEdition.BizCenter.Features.Wallet.GetWallet;
using MLMConquerorGlobalEdition.BizCenter.Features.Wallet.UpdateWallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Wallets;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IMediator _mediator;

    public WalletController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/wallet</summary>
    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWalletQuery(), ct);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<WalletDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<WalletDto>.Ok(result.Value!));
    }

    /// <summary>PUT /api/v1/bizcenter/wallet</summary>
    [HttpPut("wallet")]
    public async Task<IActionResult> UpdateWallet([FromBody] UpdateWalletRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateWalletCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<WalletDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<WalletDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/billing/history — paginated order history</summary>
    [HttpGet("billing/history")]
    public async Task<IActionResult> GetBillingHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBillingHistoryQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<OrderHistoryDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<OrderHistoryDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/billing/credit-cards</summary>
    [HttpGet("billing/credit-cards")]
    public async Task<IActionResult> GetCreditCards(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCreditCardsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<CreditCardDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<CreditCardDto>>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/bizcenter/billing/credit-cards</summary>
    [HttpPost("billing/credit-cards")]
    public async Task<IActionResult> AddCreditCard([FromBody] AddCreditCardRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddCreditCardCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CreditCardDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CreditCardDto>.Ok(result.Value!));
    }

    /// <summary>DELETE /api/v1/bizcenter/billing/credit-cards/{cardId}</summary>
    [HttpDelete("billing/credit-cards/{cardId}")]
    public async Task<IActionResult> DeleteCreditCard(string cardId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCreditCardCommand(cardId), ct);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Credit card removed successfully."));
    }

    /// <summary>PUT /api/v1/bizcenter/billing/credit-cards/{cardId}/default</summary>
    [HttpPut("billing/credit-cards/{cardId}/default")]
    public async Task<IActionResult> SetDefaultCreditCard(string cardId, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetDefaultCreditCardCommand(cardId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Default card updated."));
    }

    /// <summary>PUT /api/v1/bizcenter/billing/credit-cards/reorder — drag-drop priority order</summary>
    [HttpPut("billing/credit-cards/reorder")]
    public async Task<IActionResult> ReorderCreditCards(
        [FromBody] ReorderCreditCardsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderCreditCardsCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true, "Charge-attempt order updated."));
    }

    // ─── Multi-gateway wallet management ────────────────────────────────────
    // All wallet logic lives in IMemberWalletService (shared with AdminAPI).
    // History + per-call API logging happen automatically inside the service.

    /// <summary>GET /api/v1/bizcenter/wallets — every gateway account the member has.</summary>
    [HttpGet("wallets")]
    public async Task<IActionResult> ListWallets(
        [FromServices] IMemberWalletService svc,
        [FromServices] ICurrentUserService user,
        CancellationToken ct)
    {
        var result = await svc.GetAccountsAsync(user.MemberId, ct);
        return Ok(ApiResponse<List<WalletAccountView>>.Ok(result));
    }

    /// <summary>PUT /api/v1/bizcenter/wallets/{walletType} — create/update an account for that gateway.</summary>
    [HttpPut("wallets/{walletType}")]
    public async Task<IActionResult> SaveWallet(
        WalletType walletType,
        [FromBody] SaveWalletPayload payload,
        [FromServices] IMemberWalletService svc,
        [FromServices] ICurrentUserService user,
        CancellationToken ct)
    {
        var req = new SaveWalletRequest
        {
            WalletType               = walletType,
            AccountIdentifier        = payload.AccountIdentifier,
            Notes                    = payload.Notes,
            EWalletPasswordEncrypted = payload.EWalletPasswordEncrypted
        };
        var result = await svc.SaveAccountAsync(user.MemberId, req, user.UserId, ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<WalletAccountView>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<WalletAccountView>.Ok(result.Value!));
    }

    /// <summary>PUT /api/v1/bizcenter/wallets/{walletType}/default — mark this gateway as default.</summary>
    [HttpPut("wallets/{walletType}/default")]
    public async Task<IActionResult> SetDefault(
        WalletType walletType,
        [FromServices] IMemberWalletService svc,
        [FromServices] ICurrentUserService user,
        CancellationToken ct)
    {
        var result = await svc.SetDefaultAsync(user.MemberId, walletType, user.UserId, ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<WalletAccountView>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<WalletAccountView>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/wallets/history — full audit trail of the member's wallet changes.</summary>
    [HttpGet("wallets/history")]
    public async Task<IActionResult> GetWalletHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromServices] IMemberWalletService svc = null!,
        [FromServices] ICurrentUserService user = null!,
        CancellationToken ct = default)
    {
        var result = await svc.GetHistoryAsync(user.MemberId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<WalletHistoryView>>.Ok(result));
    }

    /// <summary>
    /// GET /api/v1/bizcenter/payment-gateways — gateway descriptions + admin fee
    /// shown to the user inside the wallet card so they understand the rules and
    /// the cost of each payout method.
    /// </summary>
    [HttpGet("payment-gateways")]
    public async Task<IActionResult> GetGatewayInfo(
        [FromServices] IMemberWalletService svc, CancellationToken ct = default)
    {
        var result = await svc.GetGatewayInfoAsync(ct);
        return Ok(ApiResponse<List<PaymentGatewayInfoView>>.Ok(result));
    }

    public record SaveWalletPayload(
        string AccountIdentifier,
        string? Notes = null,
        string? EWalletPasswordEncrypted = null);
}
