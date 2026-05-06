using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetGuestPasses;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenBalances;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenTransactions;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/tokens")]
[Authorize]
public class TokensController : ControllerBase
{
    private readonly IMediator _mediator;

    public TokensController(IMediator mediator) => _mediator = mediator;

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
}

/// <summary>Request body for instance-level token transfer.</summary>
public class TransferTokenInstanceRequest
{
    public string TokenCode { get; set; } = string.Empty;
    public string RecipientMemberId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
