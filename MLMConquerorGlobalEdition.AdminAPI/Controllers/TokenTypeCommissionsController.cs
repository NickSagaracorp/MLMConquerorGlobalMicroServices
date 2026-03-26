using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.CreateTokenTypeCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.DeleteTokenTypeCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.GetTokenTypeCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.UpdateTokenTypeCommission;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/token-type-commissions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class TokenTypeCommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TokenTypeCommissionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? tokenTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetTokenTypeCommissionsQuery(tokenTypeId, new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<TokenTypeCommissionDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<TokenTypeCommissionDto>>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTokenTypeCommissionRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateTokenTypeCommissionCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<int>.Ok(result.Value!, "Token type commission created."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTokenTypeCommissionRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateTokenTypeCommissionCommand(id, request), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Token type commission updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteTokenTypeCommissionCommand(id), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Token type commission deleted."));
    }
}
