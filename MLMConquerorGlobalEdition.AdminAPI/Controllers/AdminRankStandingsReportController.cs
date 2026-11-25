using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Lifetime Rank y Current Rank (un endpoint con el parámetro mode). Lectura y exportación a CSV para SuperAdmin y Admin.
/// No confundir con AdminRankReportsController (api/v1/admin/ranks/reports: primeros logros y antigüedad).
/// </summary>
[ApiController]
[Route("api/v1/admin/reports/ranks")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminRankStandingsReportController : ControllerBase
{
    private readonly IRankReportService _ranks;
    private readonly CsvReportWriter    _csv;
    private readonly IDateTimeProvider  _clock;

    public AdminRankStandingsReportController(IRankReportService ranks, CsvReportWriter csv, IDateTimeProvider clock)
    {
        _ranks = ranks;
        _csv   = csv;
        _clock = clock;
    }

    /// <summary>GET reports/ranks/snapshot?mode= — fecha de la foto semanal que se muestra (null: se calcula al momento).</summary>
    [HttpGet("snapshot")]
    public async Task<IActionResult> Snapshot([FromQuery] RankReportMode mode, CancellationToken ct) =>
        Ok(ApiResponse<DateTime?>.Ok(await _ranks.UltimaFotoAsync(mode, ct)));

    /// <summary>POST reports/ranks/grid?mode=&amp;rankId=&amp;statuses=&amp;sinceFrom=&amp;sinceTo= — la búsqueda va en el cuerpo.</summary>
    [HttpPost("grid")]
    public async Task<IActionResult> Grid([FromBody] GridDataRequest request, [FromQuery] RankReportMode mode,
        [FromQuery] int? rankId, [FromQuery] MemberAccountStatus[]? statuses, [FromQuery] DateTime? sinceFrom,
        [FromQuery] DateTime? sinceTo, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<RankReportRowView>>.Ok(await _ranks.GetPageAsync(
            new RankReportFilter(mode, null, rankId, statuses, sinceFrom, sinceTo), request, ct)));

    /// <summary>GET reports/ranks/export?mode=&amp;rankId=&amp;statuses=&amp;sinceFrom=&amp;sinceTo=&amp;search= — CSV.</summary>
    [HttpGet("export")]
    // Punto de enganche para acotar la exportacion a un rol propio mas adelante (permiso reports.export).
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Export([FromQuery] RankReportMode mode, [FromQuery] int? rankId,
        [FromQuery] MemberAccountStatus[]? statuses, [FromQuery] DateTime? sinceFrom, [FromQuery] DateTime? sinceTo,
        [FromQuery] string? search, CancellationToken ct)
    {
        var rows  = await _ranks.GetAllAsync(new RankReportFilter(mode, search, rankId, statuses, sinceFrom, sinceTo), ct);
        var label = mode == RankReportMode.Lifetime ? "Lifetime Rank" : "Current Rank";
        var bytes = _csv.Write(rows,
            ("Ambassador", r => r.MemberName), ("Email", r => r.Email), ("Country", r => r.Country),
            ("Status", r => r.Status.ToString()), (label, r => r.RankName), ($"{label} Since", r => r.Since));
        var file = $"{(mode == RankReportMode.Lifetime ? "lifetime" : "current")}-rank-{_clock.Now:yyyy-MM-dd}.csv";
        return File(bytes, CsvReportWriter.ContentType, file);
    }
}
