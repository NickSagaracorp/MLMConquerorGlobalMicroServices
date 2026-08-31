using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusMemberSummary;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusWeekStats;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusAmbassadors;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusBranch;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusStats;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsBreakdown;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsHistory;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsMonthBreakdown;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsSummary;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetDualResidualCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusSummary;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusSummary;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// Las comisiones del miembro que llama.
/// </summary>
/// <remarks>
/// TODAS ESTAS RUTAS SACAN EL MIEMBRO DEL TOKEN dentro de su manejador —ninguna lo lleva en la
/// URL— menos una: <c>car-bonus/ambassadors/{memberId}/branch</c>, que recibía el identificador
/// por la ruta y no miraba el token en ningún sitio. Con eso, cualquier cuenta autenticada leía el
/// desglose de la rama de cualquier embajador: nombres, nivel de membresía, caducidad y puntos de
/// toda su descendencia con suscripción activa. Es la misma familia de agujeros que cerró
/// 4f4beaf.
///
/// El sujeto legítimo aquí no es solo la cuenta propia: el informe del bono del coche despliega la
/// rama de cada embajador de la red del que mira, así que la comprobación va por
/// <see cref="IDownlineGuard"/> contra el árbol de PATROCINIO, que es el que ese informe recorre
/// —<c>GetCarBonusBranchAsync</c> filtra por <c>GenealogyTree</c>—.
/// </remarks>
[ApiController]
[Route("api/v1/bizcenter/commissions")]
[Authorize]
public class CommissionsController : ControllerBase
{
    private readonly IMediator      _mediator;
    private readonly IDownlineGuard _propiedad;

    public CommissionsController(IMediator mediator, IDownlineGuard propiedad)
    {
        _mediator  = mediator;
        _propiedad = propiedad;
    }

    /// <inheritdoc cref="TeamController"/>
    private IActionResult Ajeno() =>
        StatusCode(StatusCodes.Status403Forbidden,
            ApiResponse<bool>.Fail("FORBIDDEN", "Ese miembro no está en tu red.",
                HttpContext.TraceIdentifier));

    /// <summary>GET /api/v1/bizcenter/commissions — paged list with optional status/date filters</summary>
    [HttpGet]
    public async Task<IActionResult> GetCommissions(
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 20,
        [FromQuery] string?   status   = null,
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsQuery(page, pageSize, status, from, to), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/summary — pending, paid, and current-year totals</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CommissionSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CommissionSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/history — year/month grouped totals (paid only)</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsHistoryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CommissionHistoryYearDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CommissionHistoryYearDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/breakdown?paymentDate=&amp;earnedDate= — type breakdown. earnedDate required for pending (narrows by batch), omit for paid.</summary>
    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown(
        [FromQuery] DateTime  paymentDate,
        [FromQuery] DateTime? earnedDate = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsBreakdownQuery(paymentDate, earnedDate), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CommissionBreakdownDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CommissionBreakdownDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/month-breakdown?year=&amp;month= — monthly detail per type</summary>
    [HttpGet("month-breakdown")]
    public async Task<IActionResult> GetMonthBreakdown([FromQuery] int year, [FromQuery] int month, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCommissionsMonthBreakdownQuery(year, month), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CommissionMonthBreakdownDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CommissionMonthBreakdownDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/dual-residual — paged earnings,
    /// optionally filtered by EarnedDate year and/or month.</summary>
    [HttpGet("dual-residual")]
    public async Task<IActionResult> GetDualResidual(
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 20,
        [FromQuery] int? year     = null,
        [FromQuery] int? month    = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetDualResidualCommissionsQuery(page, pageSize, year, month), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/dual-residual/chart — last
    /// N (default 6) monthly aggregates for the residuals histogram.</summary>
    [HttpGet("dual-residual/chart")]
    public async Task<IActionResult> GetDualResidualChart(
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Commissions.ICommissionsService service,
        [FromServices] MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService currentUser,
        [FromQuery] int months = 6,
        CancellationToken ct = default)
    {
        var data = await service.GetDualResidualChartAsync(currentUser.MemberId, months, ct);
        return Ok(ApiResponse<List<MLMConquerorGlobalEdition.Repository.Services.Commissions.MonthlyAmountView>>.Ok(data));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/fast-start-bonus — paged earnings</summary>
    [HttpGet("fast-start-bonus")]
    public async Task<IActionResult> GetFastStartBonus(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFastStartBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/fast-start-bonus/summary</summary>
    [HttpGet("fast-start-bonus/summary")]
    public async Task<IActionResult> GetFastStartBonusSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFastStartBonusSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CommissionBonusSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CommissionBonusSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/boost-bonus — paged earnings</summary>
    [HttpGet("boost-bonus")]
    public async Task<IActionResult> GetBoostBonus(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBoostBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/boost-bonus/week-stats</summary>
    [HttpGet("boost-bonus/week-stats")]
    public async Task<IActionResult> GetBoostBonusWeekStats(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBoostBonusWeekStatsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<BoostBonusWeekStatsDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<BoostBonusWeekStatsDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/boost-bonus/summary</summary>
    [HttpGet("boost-bonus/summary")]
    public async Task<IActionResult> GetBoostBonusMemberSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBoostBonusMemberSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<BoostBonusMemberSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<BoostBonusMemberSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/presidential-bonus — paged earnings</summary>
    [HttpGet("presidential-bonus")]
    public async Task<IActionResult> GetPresidentialBonus(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPresidentialBonusCommissionsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/presidential-bonus/summary</summary>
    [HttpGet("presidential-bonus/summary")]
    public async Task<IActionResult> GetPresidentialBonusSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPresidentialBonusSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CommissionBonusSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CommissionBonusSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus/stats — current month progress</summary>
    [HttpGet("car-bonus/stats")]
    public async Task<IActionResult> GetCarBonusStats(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCarBonusStatsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CarBonusStatsDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CarBonusStatsDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus/ambassadors — downline breakdown</summary>
    [HttpGet("car-bonus/ambassadors")]
    public async Task<IActionResult> GetCarBonusAmbassadors(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCarBonusAmbassadorsQuery(from, to), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<CarBonusAmbassadorDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<List<CarBonusAmbassadorDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus/ambassadors/{memberId}/branch — branch member breakdown</summary>
    [HttpGet("car-bonus/ambassadors/{memberId}/branch")]
    public async Task<IActionResult> GetCarBonusBranch(
        [FromRoute] string memberId,
        CancellationToken ct = default)
    {
        if (!await _propiedad.PuedeVerRamaDePatrocinioAsync(User, memberId, ct)) return Ajeno();

        var result = await _mediator.Send(new GetCarBonusBranchQuery(memberId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CarBonusBranchDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<CarBonusBranchDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/commissions/car-bonus — paged earnings</summary>
    [HttpGet("car-bonus")]
    public async Task<IActionResult> GetCarBonus(
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 20,
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCarBonusCommissionsQuery(page, pageSize, from, to), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CommissionEarningDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<CommissionEarningDto>>.Ok(result.Value!));
    }
}
