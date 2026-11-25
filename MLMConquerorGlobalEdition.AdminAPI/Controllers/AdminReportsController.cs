using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>Reportes del admin. Lectura para SuperAdmin y Admin; exportar a CSV también solo para ellos (no IT).</summary>
[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminReportsController : ControllerBase
{
    private readonly IEarningsRankingService _earnings;
    private readonly CsvReportWriter         _csv;
    private readonly IDateTimeProvider       _clock;

    public AdminReportsController(IEarningsRankingService earnings, CsvReportWriter csv, IDateTimeProvider clock)
    {
        _earnings = earnings;
        _csv      = csv;
        _clock    = clock;
    }

    /// <summary>Sin corte: el de la última foto semanal; si todavía no hay foto, ayer.</summary>
    private async Task<DateTime> CutoffAsync(DateTime? cutoff, CancellationToken ct) =>
        (cutoff ?? await _earnings.UltimaFotoAsync(ct) ?? _clock.Now.Date.AddDays(-1)).Date;

    /// <summary>GET reports/earnings/snapshot — fecha de la última foto semanal (null si todavía no hay).</summary>
    [HttpGet("earnings/snapshot")]
    public async Task<IActionResult> EarningsSnapshot(CancellationToken ct) =>
        Ok(ApiResponse<DateTime?>.Ok(await _earnings.UltimaFotoAsync(ct)));

    /// <summary>POST reports/earnings/grid?cutoff=yyyy-MM-dd&amp;days=180 — sin days: de por vida; sin cutoff: la última foto.</summary>
    [HttpPost("earnings/grid")]
    public async Task<IActionResult> EarningsGrid([FromBody] GridDataRequest request,
        [FromQuery] DateTime? cutoff, [FromQuery] int? days, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<EarningsRankRowView>>.Ok(
            await _earnings.GetPageAsync(await CutoffAsync(cutoff, ct), days, request, ct)));

    /// <summary>GET reports/earnings/export?cutoff=&amp;days=&amp;search= — CSV con las filas filtradas.</summary>
    [HttpGet("earnings/export")]
    // Punto de enganche para acotar la exportacion a un rol propio mas adelante (permiso reports.export).
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> EarningsExport([FromQuery] DateTime? cutoff, [FromQuery] int? days,
        [FromQuery] string? search, CancellationToken ct)
    {
        var date   = await CutoffAsync(cutoff, ct);
        var window = EarningsRankingService.NormalizeWindow(days);
        var rows   = await _earnings.GetAllAsync(date, days, search, ct);
        var bytes  = _csv.Write(rows,
            ("Rank", r => r.Rank), ("Username", r => r.Username), ("Member Name", r => r.MemberName),
            ("Email", r => r.Email), ("Country", r => r.Country), ("Total", r => r.Total));
        var file = window is null ? $"earnings-lifetime-{date:yyyy-MM-dd}.csv" : $"earnings-{window}d-{date:yyyy-MM-dd}.csv";
        return File(bytes, CsvReportWriter.ContentType, file);
    }
}
