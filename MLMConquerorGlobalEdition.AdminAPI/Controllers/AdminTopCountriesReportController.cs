using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Top Countries: tabla por país, gráfica por período y exportación a CSV (SuperAdmin y Admin) sobre la foto diaria; y
/// la foto manual de hoy (solo SuperAdmin), que el job nocturno del BizCenter reemplaza con el cierre del día.
/// </summary>
[ApiController]
[Route("api/v1/admin/reports/countries")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminTopCountriesReportController : ControllerBase
{
    private readonly ITopCountriesReportService   _report;
    private readonly IMemberCountrySnapshotWriter _writer;
    private readonly CsvReportWriter              _csv;
    private readonly IDateTimeProvider            _clock;

    public AdminTopCountriesReportController(ITopCountriesReportService report, IMemberCountrySnapshotWriter writer,
        CsvReportWriter csv, IDateTimeProvider clock)
    {
        _report = report;
        _writer = writer;
        _csv    = csv;
        _clock  = clock;
    }

    private DateTime Day(DateTime? date) => (date ?? _clock.Now).Date;

    /// <summary>GET reports/countries/table?date=&amp;statuses= — sin fecha: hoy (la última foto anterior).</summary>
    [HttpGet("table")]
    public async Task<IActionResult> Table([FromQuery] DateTime? date, [FromQuery] MemberAccountStatus[]? statuses,
        CancellationToken ct) =>
        Ok(ApiResponse<TopCountriesTableView>.Ok(await _report.GetTableAsync(Day(date), statuses, ct)));

    /// <summary>GET reports/countries/chart?frequency=Monthly&amp;from=&amp;to= | Weekly&amp;date= | Daily&amp;date=.</summary>
    [HttpGet("chart")]
    public async Task<IActionResult> Chart([FromQuery] CountryChartFrequency frequency, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, [FromQuery] DateTime? date, [FromQuery] MemberAccountStatus[]? statuses,
        CancellationToken ct)
    {
        IReadOnlyList<ReportPeriod> periods;
        switch (frequency)
        {
            case CountryChartFrequency.Weekly:
                var month = Day(date);
                periods = ReportPeriods.WeeksOfMonth(month.Year, month.Month);
                break;
            case CountryChartFrequency.Daily:
                periods = ReportPeriods.DaysOfWeek(Day(date));
                break;
            default:
                var end   = Day(to);
                var start = (from ?? new DateTime(end.Year, end.Month, 1).AddMonths(-11)).Date;
                if (start > end)
                    return BadRequest(ApiResponse<object>.Fail("INVALID_RANGE", "From must be on or before To."));
                if (start < end.AddYears(-5))
                    return BadRequest(ApiResponse<object>.Fail("RANGE_TOO_LONG", "The range can cover at most five years."));
                periods = ReportPeriods.Months(start, end);
                break;
        }
        return Ok(ApiResponse<IReadOnlyList<CountryChartPointView>>.Ok(await _report.GetChartAsync(periods, statuses, ct)));
    }

    /// <summary>GET reports/countries/export?date=&amp;statuses= — CSV de la tabla.</summary>
    [HttpGet("export")]
    // Punto de enganche para acotar la exportacion a un rol propio mas adelante (permiso reports.export).
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Export([FromQuery] DateTime? date, [FromQuery] MemberAccountStatus[]? statuses,
        CancellationToken ct)
    {
        var table = await _report.GetTableAsync(Day(date), statuses, ct);
        var bytes = _csv.Write(table.Rows,
            ("Country", r => r.Country), ("Total", r => r.Total), ("Ambassadors", r => r.Ambassadors),
            ("Customers", r => r.Customers), ("Change", r => r.Change));
        var day = table.SnapshotDate ?? Day(date);
        return File(bytes, CsvReportWriter.ContentType, $"top-countries-{day:yyyy-MM-dd}.csv");
    }

    /// <summary>POST reports/countries/snapshot — foto de hoy con el estado actual (provisional hasta el cierre).</summary>
    [HttpPost("snapshot")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> TakeSnapshot(CancellationToken ct)
    {
        var day  = _clock.Now.Date;
        var rows = await _writer.WriteAsync(day, ct);
        return Ok(ApiResponse<object>.Ok(new { SnapshotDate = day, Rows = rows }));
    }
}
