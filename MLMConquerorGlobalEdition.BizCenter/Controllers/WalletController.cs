using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.AddCreditCard;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.DeleteCreditCard;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetBillingHistory;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetCreditCards;
using MLMConquerorGlobalEdition.BizCenter.Features.Wallet.GetWallet;
using MLMConquerorGlobalEdition.BizCenter.Features.Wallet.UpdateWallet;
using MLMConquerorGlobalEdition.SharedKernel;

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
}
