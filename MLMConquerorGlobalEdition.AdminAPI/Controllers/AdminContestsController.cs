using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// CRUD + leaderboard endpoints for <see cref="CorporateContest"/>.
/// All routes are admin-scoped — the BizCenter has its own
/// <c>/api/v1/bizcenter/contests/active</c> endpoint that delivers a
/// localized, ambassador-facing slice of the same data.
/// </summary>
[ApiController]
[Route("api/v1/admin/contests")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminContestsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminContestsController(AppDbContext db) => _db = db;

    // ─── List + detail ───────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        var rows = await _db.CorporateContests.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new ContestDto
            {
                Id                = c.Id,
                Name              = c.Name,
                Description       = c.Description,
                StartDate         = c.StartDate,
                EndDate           = c.EndDate,
                BannerUrl         = c.BannerUrl,
                RulesUrl          = c.RulesUrl,
                TopX              = c.TopX,
                IsActive          = c.IsActive,
                PointsBoxXPercent = c.PointsBoxXPercent,
                PointsBoxYPercent = c.PointsBoxYPercent
            })
            .ToListAsync(ct);
        return Ok(ApiResponse<List<ContestDto>>.Ok(rows));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct = default)
    {
        var c = await _db.CorporateContests.AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (c is null) return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Contest not found."));

        return Ok(ApiResponse<ContestDetailDto>.Ok(new ContestDetailDto
        {
            Id                = c.Id,
            Name              = c.Name,
            Description       = c.Description,
            StartDate         = c.StartDate,
            EndDate           = c.EndDate,
            BannerUrl         = c.BannerUrl,
            RulesUrl          = c.RulesUrl,
            TopX              = c.TopX,
            IsActive          = c.IsActive,
            PointsBoxXPercent = c.PointsBoxXPercent,
            PointsBoxYPercent = c.PointsBoxYPercent,
            Translations = c.Translations.Select(t => new ContestTranslationDto
            {
                Id           = t.Id,
                LanguageCode = t.LanguageCode,
                Name         = t.Name,
                Description  = t.Description,
                BannerUrl    = t.BannerUrl
            }).ToList()
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertContestRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(ApiResponse<object>.Fail("NAME_REQUIRED", "Name is required."));
        if (req.StartDate >= req.EndDate)
            return BadRequest(ApiResponse<object>.Fail("INVALID_RANGE", "Start date must be before end date."));

        var now = DateTime.UtcNow;
        var entity = new CorporateContest
        {
            Name              = req.Name.Trim(),
            Description       = req.Description,
            StartDate         = req.StartDate,
            EndDate           = req.EndDate,
            BannerUrl         = req.BannerUrl,
            RulesUrl          = req.RulesUrl,
            TopX              = req.TopX > 0 ? req.TopX : 10,
            IsActive          = req.IsActive,
            PointsBoxXPercent = ClampPct(req.PointsBoxXPercent),
            PointsBoxYPercent = ClampPct(req.PointsBoxYPercent),
            CreationDate      = now,
            CreatedBy         = User.Identity?.Name ?? "admin",
            LastUpdateDate    = now
        };
        await _db.CorporateContests.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<string>.Ok(entity.Id, "Contest created."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpsertContestRequest req, CancellationToken ct = default)
    {
        var entity = await _db.CorporateContests.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Contest not found."));
        if (req.StartDate >= req.EndDate)
            return BadRequest(ApiResponse<object>.Fail("INVALID_RANGE", "Start date must be before end date."));

        entity.Name              = req.Name.Trim();
        entity.Description       = req.Description;
        entity.StartDate         = req.StartDate;
        entity.EndDate           = req.EndDate;
        entity.BannerUrl         = req.BannerUrl;
        entity.RulesUrl          = req.RulesUrl;
        entity.TopX              = req.TopX > 0 ? req.TopX : 10;
        entity.IsActive          = req.IsActive;
        entity.PointsBoxXPercent = ClampPct(req.PointsBoxXPercent);
        entity.PointsBoxYPercent = ClampPct(req.PointsBoxYPercent);
        entity.LastUpdateDate    = DateTime.UtcNow;
        entity.LastUpdateBy      = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { entity.Id }, "Contest updated."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var entity = await _db.CorporateContests.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Contest not found."));
        // Soft-delete preserves leaderboard history.
        entity.IsDeleted      = true;
        entity.IsActive       = false;
        entity.DeletedAt      = DateTime.UtcNow;
        entity.DeletedBy      = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Contest archived."));
    }

    // ─── Translations CRUD (one row per language, upsert by language code) ───
    [HttpPut("{id}/translations/{languageCode}")]
    public async Task<IActionResult> UpsertTranslation(
        string id, string languageCode,
        [FromBody] UpsertContestTranslationRequest req,
        CancellationToken ct = default)
    {
        var contest = await _db.CorporateContests.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (contest is null) return NotFound(ApiResponse<object>.Fail("CONTEST_NOT_FOUND", "Contest not found."));

        var lang = languageCode.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lang) || lang.Length > 10)
            return BadRequest(ApiResponse<object>.Fail("INVALID_LANG", "Language code must be a non-empty short code."));

        var existing = await _db.CorporateContestTranslations
            .FirstOrDefaultAsync(t => t.ContestId == id && t.LanguageCode == lang, ct);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            existing = new CorporateContestTranslation
            {
                ContestId      = id,
                LanguageCode   = lang,
                Name           = req.Name,
                Description    = req.Description,
                BannerUrl      = req.BannerUrl,
                CreationDate   = now,
                CreatedBy      = User.Identity?.Name ?? "admin",
                LastUpdateDate = now
            };
            await _db.CorporateContestTranslations.AddAsync(existing, ct);
        }
        else
        {
            existing.Name           = req.Name;
            existing.Description    = req.Description;
            existing.BannerUrl      = req.BannerUrl;
            existing.LastUpdateDate = now;
            existing.LastUpdateBy   = User.Identity?.Name ?? "admin";
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { existing.Id, existing.LanguageCode }, "Translation saved."));
    }

    [HttpDelete("{id}/translations/{languageCode}")]
    public async Task<IActionResult> DeleteTranslation(string id, string languageCode, CancellationToken ct = default)
    {
        var lang = languageCode.Trim().ToLowerInvariant();
        var existing = await _db.CorporateContestTranslations
            .FirstOrDefaultAsync(t => t.ContestId == id && t.LanguageCode == lang, ct);
        if (existing is null) return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Translation not found."));

        _db.CorporateContestTranslations.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { id, languageCode = lang }, "Translation removed."));
    }

    // ─── Leaderboard ─────────────────────────────────────────────────────────
    [HttpGet("{id}/leaderboard")]
    public async Task<IActionResult> Leaderboard(
        string id,
        [FromQuery] int? top = null,
        CancellationToken ct = default)
    {
        var contest = await _db.CorporateContests.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (contest is null) return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Contest not found."));

        var limit = top.HasValue && top.Value > 0 ? top.Value : contest.TopX;

        var rows = await _db.CorporateContestEarnings.AsNoTracking()
            .Where(e => e.ContestId == id)
            .GroupBy(e => e.BeneficiaryMemberId)
            .Select(g => new
            {
                MemberId   = g.Key,
                TotalPoints = g.Sum(x => x.Points),
                Signups    = g.Select(x => x.SourceMemberId).Distinct().Count()
            })
            .OrderByDescending(x => x.TotalPoints)
            .Take(limit)
            .ToListAsync(ct);

        var memberIds = rows.Select(r => r.MemberId).ToList();
        var members = await _db.MemberProfiles.AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName, m.Country })
            .ToDictionaryAsync(m => m.MemberId, ct);

        var leaderboard = rows
            .Select((r, i) => new ContestLeaderboardRowDto
            {
                Rank        = i + 1,
                MemberId    = r.MemberId,
                FullName    = members.TryGetValue(r.MemberId, out var p) ? p.FullName : r.MemberId,
                Country     = members.TryGetValue(r.MemberId, out var p2) ? p2.Country : null,
                TotalPoints = r.TotalPoints,
                Signups     = r.Signups
            })
            .ToList();

        return Ok(ApiResponse<ContestLeaderboardDto>.Ok(new ContestLeaderboardDto
        {
            ContestId   = contest.Id,
            ContestName = contest.Name,
            StartDate   = contest.StartDate,
            EndDate     = contest.EndDate,
            TopX        = contest.TopX,
            Rows        = leaderboard
        }));
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────
    public class ContestDto
    {
        public string  Id          { get; set; } = string.Empty;
        public string  Name        { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate  { get; set; }
        public DateTime EndDate    { get; set; }
        public string? BannerUrl   { get; set; }
        public string? RulesUrl    { get; set; }
        public int     TopX        { get; set; }
        public bool    IsActive    { get; set; }
        public int     PointsBoxXPercent { get; set; } = 50;
        public int     PointsBoxYPercent { get; set; } = 50;
    }

    public class ContestDetailDto : ContestDto
    {
        public List<ContestTranslationDto> Translations { get; set; } = new();
    }

    public class ContestTranslationDto
    {
        public int     Id           { get; set; }
        public string  LanguageCode { get; set; } = string.Empty;
        public string? Name         { get; set; }
        public string? Description  { get; set; }
        public string? BannerUrl    { get; set; }
    }

    /// <summary>Clamps an admin-provided percentage to [0..100] so a typo
    /// can't push the overlay off-screen.</summary>
    private static int ClampPct(int value) => value < 0 ? 0 : value > 100 ? 100 : value;

    public class UpsertContestRequest
    {
        public string  Name        { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate  { get; set; }
        public DateTime EndDate    { get; set; }
        public string? BannerUrl   { get; set; }
        public string? RulesUrl    { get; set; }
        public int     TopX        { get; set; } = 10;
        public bool    IsActive    { get; set; } = true;
        public int     PointsBoxXPercent { get; set; } = 50;
        public int     PointsBoxYPercent { get; set; } = 50;
    }

    public class UpsertContestTranslationRequest
    {
        public string? Name        { get; set; }
        public string? Description { get; set; }
        public string? BannerUrl   { get; set; }
    }

    public class ContestLeaderboardDto
    {
        public string  ContestId   { get; set; } = string.Empty;
        public string  ContestName { get; set; } = string.Empty;
        public DateTime StartDate  { get; set; }
        public DateTime EndDate    { get; set; }
        public int     TopX        { get; set; }
        public List<ContestLeaderboardRowDto> Rows { get; set; } = new();
    }

    public class ContestLeaderboardRowDto
    {
        public int    Rank        { get; set; }
        public string MemberId    { get; set; } = string.Empty;
        public string FullName    { get; set; } = string.Empty;
        public string? Country    { get; set; }
        public int    TotalPoints { get; set; }
        public int    Signups     { get; set; }
    }
}
