using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Reports;
using MLMConquerorGlobalEdition.AdminAPI.Features.Reports.GetRankPromotions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class ReportsController : ControllerBase
{
    private readonly ISender _mediator;

    public ReportsController(ISender mediator) => _mediator = mediator;

    /// <summary>
    /// Returns ambassadors who achieved a rank for the FIRST TIME within the date range.
    /// Ambassadors who previously held the same rank and re-achieved it are excluded.
    /// </summary>
    /// <param name="from">Range start date (inclusive, UTC).</param>
    /// <param name="to">Range end date (inclusive, UTC).</param>
    /// <param name="rankDefinitionId">Optional — filter by a specific rank.</param>
    [HttpGet("rank-promotions")]
    public async Task<IActionResult> GetRankPromotions(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int?     rankDefinitionId = null,
        CancellationToken    ct = default)
    {
        if (from > to)
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_DATE_RANGE", "'from' must be earlier than or equal to 'to'."));

        var result = await _mediator.Send(
            new GetRankPromotionsQuery(from, to, rankDefinitionId), ct);

        return result.IsSuccess
            ? Ok(ApiResponse<IEnumerable<RankPromotionDto>>.Ok(result.Value!))
            : BadRequest(ApiResponse<IEnumerable<RankPromotionDto>>.Fail(result.ErrorCode!, result.Error!));
    }
}
