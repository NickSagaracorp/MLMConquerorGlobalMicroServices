using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Parámetros de Club Recognition: niveles (monto, ventana, premio) y opciones de reconocimiento, más las listas que usa
/// la captura (lookups). No se borra nada: se desactiva, porque los registros ya creados los referencian.
/// </summary>
[ApiController]
[Route("api/v1/admin/club-recognition")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminClubRecognitionParametersController : ControllerBase
{
    public const int MaxWindowDays = 3650;

    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _clock;
    private readonly ICurrentUserService _currentUser;

    public AdminClubRecognitionParametersController(AppDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        _db          = db;
        _clock       = clock;
        _currentUser = currentUser;
    }

    public sealed record LevelRequest(string Name, decimal Threshold, int? WindowDays, string? Reward, bool RewardNeedsSize,
        int SortOrder, bool IsActive = true);

    public sealed record OptionRequest(string Name, bool RequiresReason, int SortOrder, bool IsActive = true);

    public sealed record LevelDto(int Id, string Name, decimal Threshold, int? WindowDays, string? Reward,
        bool RewardNeedsSize, int SortOrder, bool IsActive);

    public sealed record OptionDto(int Id, string Name, bool RequiresReason, int SortOrder, bool IsActive);

    public sealed record EventDto(string Id, string Title, DateTime EventDate);

    public sealed record LookupsDto(IReadOnlyList<LevelDto> Levels, IReadOnlyList<OptionDto> Options,
        IReadOnlyList<EventDto> Events);

    // ── Niveles ──────────────────────────────────────────────────────────────

    [HttpGet("levels")]
    public async Task<IActionResult> Levels(CancellationToken ct) =>
        Ok(ApiResponse<List<LevelDto>>.Ok(await LevelsAsync(ct)));

    [HttpPost("levels")]
    public async Task<IActionResult> CreateLevel([FromBody] LevelRequest request, CancellationToken ct)
    {
        var error = await ValidateLevelAsync(request, null, ct);
        if (error is not null) return BadRequest(error);

        var level = new RecognitionLevel { CreationDate = _clock.Now, CreatedBy = _currentUser.UserId };
        Apply(level, request);
        _db.RecognitionLevels.Add(level);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<LevelDto>.Ok(ToDto(level)));
    }

    [HttpPut("levels/{id:int}")]
    public async Task<IActionResult> UpdateLevel(int id, [FromBody] LevelRequest request, CancellationToken ct)
    {
        var level = await _db.RecognitionLevels.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (level is null) return NotFound(Fail("LEVEL_NOT_FOUND", "Recognition level not found."));

        var error = await ValidateLevelAsync(request, id, ct);
        if (error is not null) return BadRequest(error);

        Apply(level, request);
        level.LastUpdateDate = _clock.Now;
        level.LastUpdateBy   = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<LevelDto>.Ok(ToDto(level)));
    }

    // ── Opciones ─────────────────────────────────────────────────────────────

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Ok(ApiResponse<List<OptionDto>>.Ok(await OptionsAsync(ct)));

    [HttpPost("options")]
    public async Task<IActionResult> CreateOption([FromBody] OptionRequest request, CancellationToken ct)
    {
        var error = await ValidateOptionAsync(request, null, ct);
        if (error is not null) return BadRequest(error);

        var option = new RecognitionOption { CreationDate = _clock.Now, CreatedBy = _currentUser.UserId };
        Apply(option, request);
        _db.RecognitionOptions.Add(option);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<OptionDto>.Ok(ToDto(option)));
    }

    [HttpPut("options/{id:int}")]
    public async Task<IActionResult> UpdateOption(int id, [FromBody] OptionRequest request, CancellationToken ct)
    {
        var option = await _db.RecognitionOptions.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (option is null) return NotFound(Fail("OPTION_NOT_FOUND", "Recognition option not found."));

        var error = await ValidateOptionAsync(request, id, ct);
        if (error is not null) return BadRequest(error);

        Apply(option, request);
        option.LastUpdateDate = _clock.Now;
        option.LastUpdateBy   = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<OptionDto>.Ok(ToDto(option)));
    }

    // ── Listas para la captura y los filtros ────────────────────────────────

    /// <summary>Todos los niveles y opciones (con IsActive: la captura muestra la opción inactiva que un registro ya tiene)
    /// y los eventos corporativos no borrados, del más reciente al más antiguo.</summary>
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        var events = await _db.CorporateEvents.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EventDate)
            .Select(e => new EventDto(e.Id, e.Title, e.EventDate))
            .ToListAsync(ct);
        return Ok(ApiResponse<LookupsDto>.Ok(new LookupsDto(await LevelsAsync(ct), await OptionsAsync(ct), events)));
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────

    private Task<List<LevelDto>> LevelsAsync(CancellationToken ct) =>
        _db.RecognitionLevels.AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .Select(l => new LevelDto(l.Id, l.Name, l.Threshold, l.WindowDays, l.Reward, l.RewardNeedsSize, l.SortOrder, l.IsActive))
            .ToListAsync(ct);

    private Task<List<OptionDto>> OptionsAsync(CancellationToken ct) =>
        _db.RecognitionOptions.AsNoTracking()
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
            .Select(o => new OptionDto(o.Id, o.Name, o.RequiresReason, o.SortOrder, o.IsActive))
            .ToListAsync(ct);

    private async Task<ApiResponse<object>?> ValidateLevelAsync(LevelRequest r, int? id, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
            return Fail("INVALID_NAME", "Name is required (100 characters max).");
        if (r.Threshold <= 0)
            return Fail("INVALID_THRESHOLD", "Threshold must be greater than zero.");
        if (r.WindowDays is < 1 or > MaxWindowDays)
            return Fail("INVALID_WINDOW", $"Window must be between 1 and {MaxWindowDays} days, or empty for lifetime.");
        if (r.Reward?.Trim().Length > 200)
            return Fail("INVALID_REWARD", "Reward is 200 characters max.");
        if (await _db.RecognitionLevels.AnyAsync(l => l.Name == name && l.Id != id, ct))
            return Fail("LEVEL_NAME_TAKEN", "Another level already has that name.");
        return null;
    }

    private async Task<ApiResponse<object>?> ValidateOptionAsync(OptionRequest r, int? id, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
            return Fail("INVALID_NAME", "Name is required (100 characters max).");
        if (await _db.RecognitionOptions.AnyAsync(o => o.Name == name && o.Id != id, ct))
            return Fail("OPTION_NAME_TAKEN", "Another option already has that name.");
        return null;
    }

    private static void Apply(RecognitionLevel level, LevelRequest r)
    {
        level.Name            = r.Name.Trim();
        level.Threshold       = r.Threshold;
        level.WindowDays      = r.WindowDays;
        level.Reward          = string.IsNullOrWhiteSpace(r.Reward) ? null : r.Reward.Trim();
        level.RewardNeedsSize = r.RewardNeedsSize;
        level.SortOrder       = r.SortOrder;
        level.IsActive        = r.IsActive;
    }

    private static void Apply(RecognitionOption option, OptionRequest r)
    {
        option.Name           = r.Name.Trim();
        option.RequiresReason = r.RequiresReason;
        option.SortOrder      = r.SortOrder;
        option.IsActive       = r.IsActive;
    }

    private static LevelDto ToDto(RecognitionLevel l) =>
        new(l.Id, l.Name, l.Threshold, l.WindowDays, l.Reward, l.RewardNeedsSize, l.SortOrder, l.IsActive);

    private static OptionDto ToDto(RecognitionOption o) => new(o.Id, o.Name, o.RequiresReason, o.SortOrder, o.IsActive);

    private static ApiResponse<object> Fail(string code, string message) => ApiResponse<object>.Fail(code, message);
}
