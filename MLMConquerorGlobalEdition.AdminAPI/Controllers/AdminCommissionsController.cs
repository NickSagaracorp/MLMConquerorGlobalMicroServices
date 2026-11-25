using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.BackfillFsbCountdowns;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CancelCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CreateCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissionsGrid;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetCommissionStats;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.PayCommissions;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminCommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminCommissionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("commissions")]
    public async Task<IActionResult> GetCommissions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAdminCommissionsQuery(new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminCommissionDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminCommissionDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// Lectura del grid de servidor de la pantalla de comisiones. El CustomAdaptor de Syncfusion manda en el cuerpo
    /// la página, el orden y los filtros de las columnas, y se aplican en SQL sobre toda la tabla, no solo sobre la
    /// página cargada. <paramref name="status"/> es el desplegable de la cabecera. El GET de arriba se queda para
    /// quien lo siga usando.
    /// </summary>
    [HttpPost("commissions/grid")]
    public async Task<IActionResult> GetCommissionsGrid(
        [FromBody] GridDataRequest request,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminCommissionsGridQuery(request, status), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminCommissionDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminCommissionDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// Contadores por estado de toda la tabla para la cabecera de la pantalla, aparte del grid para que paginar o
    /// filtrar no repita el agrupado. En caché unos minutos; <paramref name="bypassCache"/> es el "Refresh".
    /// </summary>
    [HttpGet("commissions/stats")]
    public async Task<IActionResult> GetCommissionStats(
        [FromQuery] bool bypassCache = false,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionStatsQuery(bypassCache), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CommissionStatsDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CommissionStatsDto>.Ok(result.Value!));
    }

    [HttpPost("commissions")]
    public async Task<IActionResult> CreateCommission(
        [FromBody] CreateCommissionRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateCommissionCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminCommissionDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminCommissionDto>.Ok(result.Value!));
    }

    [HttpDelete("commissions/{commissionId}")]
    public async Task<IActionResult> CancelCommission(string commissionId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelCommissionCommand(commissionId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Commission cancelled."));
    }

    [HttpPost("commissions/pay")]
    public async Task<IActionResult> PayCommissions(
        [FromBody] PayCommissionsRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new PayCommissionsCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<int>.Ok(result.Value!, $"{result.Value} commission(s) marked as paid."));
    }

    [HttpPost("commissions/import-csv")]
    public IActionResult ImportCsv()
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            ApiResponse<object>.Fail("NOT_IMPLEMENTED", "CSV import is not yet implemented in this iteration."));
    }

    /// <summary>
    /// POST /api/v1/admin/commissions/fsb-countdown/backfill
    /// Creates FSB countdown records for all ambassadors that don't have one yet.
    /// Safe to re-run — skips ambassadors that already have a record.
    /// </summary>
    [HttpPost("commissions/fsb-countdown/backfill")]
    public async Task<IActionResult> BackfillFsbCountdowns(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new BackfillFsbCountdownsCommand(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<BackfillFsbCountdownsResult>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<BackfillFsbCountdownsResult>.Ok(
            result.Value!,
            $"{result.Value!.Created} countdown record(s) created."));
    }
}
