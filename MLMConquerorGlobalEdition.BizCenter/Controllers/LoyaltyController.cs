using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Loyalty;
using MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;

    public LoyaltyController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/loyalty — paginated loyalty points for current member</summary>
    [HttpGet("loyalty")]
    public async Task<IActionResult> GetLoyaltyPoints([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLoyaltyPointsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<LoyaltyPointsDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<LoyaltyPointsDto>>.Ok(result.Value!));
    }
}
