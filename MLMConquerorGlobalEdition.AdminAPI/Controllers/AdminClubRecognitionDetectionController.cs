using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// POST api/v1/admin/club-recognition/detection/history — relanza la carga histórica de Club Recognition hasta ayer (hora
/// del servidor). Solo añade lo que falta, con el día real en que se cruzó cada umbral; no cambia ni borra registros.
/// Sirve tras crear un nivel o cambiar un umbral. Solo SuperAdmin: recorre todas las comisiones.
/// </summary>
[ApiController]
[Route("api/v1/admin/club-recognition/detection")]
[Authorize(Roles = "SuperAdmin")]
public class AdminClubRecognitionDetectionController : ControllerBase
{
    private readonly IClubRecognitionDetector _detector;
    private readonly IDateTimeProvider        _clock;

    public AdminClubRecognitionDetectionController(IClubRecognitionDetector detector, IDateTimeProvider clock)
    {
        _detector = detector;
        _clock    = clock;
    }

    public sealed record HistoryResult(DateTime LastDay, int Added);

    [HttpPost("history")]
    public async Task<IActionResult> LoadHistory(CancellationToken ct)
    {
        var lastDay = _clock.Now.Date.AddDays(-1);
        // Sin el token de la petición: si el navegador deja de esperar, la carga termina igual y no queda a medias.
        var added = await _detector.LoadHistoryAsync(lastDay, CancellationToken.None);
        return Ok(ApiResponse<HistoryResult>.Ok(new HistoryResult(lastDay, added)));
    }
}
