using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Pestaña «Recognition» del perfil: los niveles de Club Recognition que alcanzó el miembro y la captura de su
/// reconocimiento. CEO Call = un mes (se guarda el día 1) o una opción del catálogo; evento = un CorporateEvent o una
/// opción; talla del premio (solo si el nivel la lleva) y notas. Cada cambio real deja una fila en GeneralAuditTracking
/// con los valores de antes y después.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/recognitions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberRecognitionController : ControllerBase
{
    public const string AuditEntity   = "MemberRecognition";
    public const string AuditAction   = "RecognitionCaptured";
    public const int    MaxReason     = 300;
    public const int    MaxRewardSize = 20;
    public const int    MaxNotes      = 1000;

    private readonly AppDbContext                  _db;
    private readonly IClubRecognitionReportService _reports;
    private readonly IDateTimeProvider             _clock;
    private readonly ICurrentUserService           _currentUser;

    public AdminMemberRecognitionController(AppDbContext db, IClubRecognitionReportService reports,
        IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        _db          = db;
        _reports     = reports;
        _clock       = clock;
        _currentUser = currentUser;
    }

    public sealed record CaptureRequest(
        DateTime? CeoCallMonth, int? CeoCallOptionId, string? CeoCallReason,
        string? EventId, int? EventOptionId, string? EventReason,
        string? RewardSize, string? Notes);

    /// <summary>Lo que se audita (antes y después).</summary>
    private sealed record Snapshot(
        DateTime? CeoCallMonth, int? CeoCallOptionId, string? CeoCallReason,
        string? EventId, int? EventOptionId, string? EventReason,
        string? RewardSize, string? Notes);

    [HttpGet]
    public async Task<IActionResult> List(string memberId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ClubRecognitionRowView>>.Ok(await _reports.GetForMemberAsync(memberId, ct)));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Capture(string memberId, long id, [FromBody] CaptureRequest request, CancellationToken ct)
    {
        var record = await _db.MemberRecognitions.Include(r => r.Level)
            .FirstOrDefaultAsync(r => r.Id == id && r.MemberId == memberId, ct);
        if (record is null)
            return NotFound(Fail("RECOGNITION_NOT_FOUND", "Recognition record not found."));

        if (request.CeoCallMonth is not null && request.CeoCallOptionId is not null)
            return BadRequest(Fail("CEO_CALL_CONFLICT", "Choose the CEO call month or an option, not both."));
        var eventId = Clean(request.EventId);
        if (eventId is not null && request.EventOptionId is not null)
            return BadRequest(Fail("EVENT_CONFLICT", "Choose an event or an option, not both."));
        if (eventId is not null && !await _db.CorporateEvents.AnyAsync(e => e.Id == eventId, ct))
            return BadRequest(Fail("EVENT_NOT_FOUND", "Corporate event not found."));

        var (ceoReason, ceoError) = await ReasonAsync(request.CeoCallOptionId, request.CeoCallReason, record.CeoCallOptionId, ct);
        if (ceoError is not null) return BadRequest(ceoError);
        var (eventReason, eventError) = await ReasonAsync(request.EventOptionId, request.EventReason, record.EventOptionId, ct);
        if (eventError is not null) return BadRequest(eventError);

        var rewardSize = record.Level!.RewardNeedsSize ? Clean(request.RewardSize) : null;
        if (rewardSize?.Length > MaxRewardSize)
            return BadRequest(Fail("REWARD_SIZE_TOO_LONG", $"Reward size is {MaxRewardSize} characters max."));
        var notes = Clean(request.Notes);
        if (notes?.Length > MaxNotes)
            return BadRequest(Fail("NOTES_TOO_LONG", $"Notes are {MaxNotes} characters max."));

        var before = Snap(record);
        var after  = new Snapshot(
            request.CeoCallMonth is { } month ? new DateTime(month.Year, month.Month, 1) : (DateTime?)null,
            request.CeoCallOptionId, ceoReason, eventId, request.EventOptionId, eventReason, rewardSize, notes);

        if (after != before)
        {
            var now = _clock.Now;
            _db.AuditTracking.Add(new GeneralAuditTracking
            {
                EntityName = AuditEntity,
                EntityId   = record.Id.ToString(CultureInfo.InvariantCulture),
                Action     = AuditAction,
                OldValues  = JsonSerializer.Serialize(before),
                NewValues  = JsonSerializer.Serialize(after),
                ChangedBy  = _currentUser.UserId,
                ChangedAt  = now
            });
            record.CeoCallMonth    = after.CeoCallMonth;
            record.CeoCallOptionId = after.CeoCallOptionId;
            record.CeoCallReason   = after.CeoCallReason;
            record.EventId         = after.EventId;
            record.EventOptionId   = after.EventOptionId;
            record.EventReason     = after.EventReason;
            record.RewardSize      = after.RewardSize;
            record.Notes           = after.Notes;
            record.LastUpdateDate  = now;
            record.LastUpdateBy    = _currentUser.UserId;
            await _db.SaveChangesAsync(ct);
        }

        var row = (await _reports.GetForMemberAsync(memberId, ct)).Single(r => r.Id == id);
        return Ok(ApiResponse<ClubRecognitionRowView>.Ok(row));
    }

    /// <summary>
    /// Valida la opción elegida y devuelve el motivo que se guarda: el texto si la opción lo pide (obligatorio), null si
    /// no. Una opción inactiva solo vale si es la que el registro ya tenía.
    /// </summary>
    private async Task<(string? Reason, ApiResponse<object>? Error)> ReasonAsync(int? optionId, string? reason,
        int? currentOptionId, CancellationToken ct)
    {
        if (optionId is null) return (null, null);

        var option = await _db.RecognitionOptions.AsNoTracking().FirstOrDefaultAsync(o => o.Id == optionId, ct);
        if (option is null || (!option.IsActive && optionId != currentOptionId))
            return (null, Fail("OPTION_NOT_FOUND", "Recognition option not found or inactive."));
        if (!option.RequiresReason) return (null, null);

        var text = Clean(reason);
        if (text is null)
            return (null, Fail("REASON_REQUIRED", $"'{option.Name}' needs a reason."));
        if (text.Length > MaxReason)
            return (null, Fail("REASON_TOO_LONG", $"Reason is {MaxReason} characters max."));
        return (text, null);
    }

    private static Snapshot Snap(MemberRecognition r) => new(
        r.CeoCallMonth, r.CeoCallOptionId, r.CeoCallReason, r.EventId, r.EventOptionId, r.EventReason, r.RewardSize, r.Notes);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ApiResponse<object> Fail(string code, string message) => ApiResponse<object>.Fail(code, message);
}
