using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetDualResidualCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusCommissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/commissions")]
[Authorize]
public class CommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommissionsController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/commissions — paged list of all commission earnings</summary>
    [HttpGet]
    public async Task<IActionResult> GetCommissions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/dual-residual</summary>
    [HttpGet("dual-residual")]
    public async Task<IActionResult> GetDualResidual([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDualResidualCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/fast-start-bonus</summary>
    [HttpGet("fast-start-bonus")]
    public async Task<IActionResult> GetFastStartBonus([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFastStartBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/boost-bonus</summary>
    [HttpGet("boost-bonus")]
    public async Task<IActionResult> GetBoostBonus([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBoostBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/presidential-bonus</summary>
    [HttpGet("presidential-bonus")]
    public async Task<IActionResult> GetPresidentialBonus([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPresidentialBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }
}
