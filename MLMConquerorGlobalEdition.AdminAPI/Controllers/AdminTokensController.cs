using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetAdminTokenBalances;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetTokenCodes;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GrantTokens;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.UpdateTokenBalance;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/tokens")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminTokensController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminTokensController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetTokenBalances(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? memberId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAdminTokenBalancesQuery(new PagedRequest { Page = page, PageSize = pageSize }, memberId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminTokenBalanceDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminTokenBalanceDto>>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> GrantTokens(
        [FromBody] AdminGrantTokenRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GrantTokensCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminTokenBalanceDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTokenBalanceDto>.Ok(result.Value!));
    }

    [HttpGet("{memberId}/codes")]
    public async Task<IActionResult> GetTokenCodes(
        string memberId,
        [FromQuery] int? tokenTypeId = null,
        [FromQuery] bool? isUsed = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetTokenCodesQuery(memberId, tokenTypeId, isUsed, page, pageSize), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<TokenCodeDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<TokenCodeDto>>.Ok(result.Value!));
    }

    [HttpPut("{tokenId}")]
    public async Task<IActionResult> UpdateTokenBalance(
        string tokenId,
        [FromBody] AdminUpdateTokenBalanceRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateTokenBalanceCommand(tokenId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminTokenBalanceDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTokenBalanceDto>.Ok(result.Value!));
    }
}
