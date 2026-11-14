using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Vistas de Club Recognition (All Records, Not Recognized Online, Not Recognized on Events, Recognized Online,
/// Recognized Events: un endpoint con el parámetro view). Lectura y exportación a CSV para SuperAdmin y Admin.
/// </summary>
[ApiController]
[Route("api/v1/admin/reports/club-recognition")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminClubRecognitionReportController : ControllerBase
{
    private readonly IClubRecognitionReportService _reports;
    private readonly CsvReportWriter               _csv;
    private readonly IDateTimeProvider             _clock;

    public AdminClubRecognitionReportController(IClubRecognitionReportService reports, CsvReportWriter csv,
        IDateTimeProvider clock)
    {
        _reports = reports;
        _csv     = csv;
        _clock   = clock;
    }

    /// <summary>POST reports/club-recognition/grid?view=&amp;levelId= — la búsqueda va en el cuerpo.</summary>
    [HttpPost("grid")]
    public async Task<IActionResult> Grid([FromBody] GridDataRequest request, [FromQuery] ClubRecognitionView view,
        [FromQuery] int? levelId, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<ClubRecognitionRowView>>.Ok(
            await _reports.GetPageAsync(new ClubRecognitionFilter(view, levelId), request, ct)));

    /// <summary>GET reports/club-recognition/export?view=&amp;levelId=&amp;search= — CSV con las filas filtradas.</summary>
    [HttpGet("export")]
    // Punto de enganche para acotar la exportacion a un rol propio mas adelante (permiso reports.export).
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Export([FromQuery] ClubRecognitionView view, [FromQuery] int? levelId,
        [FromQuery] string? search, CancellationToken ct)
    {
        var rows  = await _reports.GetAllAsync(new ClubRecognitionFilter(view, levelId, search), ct);
        var bytes = _csv.Write(rows,
            ("Ambassador", r => r.MemberName), ("Email", r => r.Email), ("Username", r => r.Username),
            ("Country", r => r.Country), ("Level", r => r.LevelName), ("Reached", r => r.ReachedDate),
            ("Total", r => r.QualifyingTotal), ("CEO Call Recognized", r => r.CeoCallRecognized),
            ("Event Recognized", r => r.EventRecognized), ("Reward Size", r => r.RewardSize));
        var file = $"club-recognition-{ClubRecognitionViewNames.Slug(view)}-{_clock.Now:yyyy-MM-dd}.csv";
        return File(bytes, CsvReportWriter.ContentType, file);
    }
}
