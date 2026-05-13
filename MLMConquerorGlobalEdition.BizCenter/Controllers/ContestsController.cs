using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// Member-facing contest endpoints. The dashboard widget calls
/// <c>GET /active</c> to render every currently-running contest with
/// localized banner / name / description, the top-N leaderboard, the
/// viewer's points, and their rank in the full ledger (not capped at TopX —
/// the viewer always knows where they stand even when off the visible list).
/// </summary>
[ApiController]
[Route("api/v1/bizcenter/contests")]
[Authorize]
public class ContestsController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public ContestsController(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct = default)
    {
        var now      = DateTime.UtcNow;
        var memberId = _currentUser.MemberId;
        var langCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

        var contests = await _db.CorporateContests.AsNoTracking()
            .Include(c => c.Translations)
            .Where(c => c.IsActive && !c.IsDeleted
                     && c.StartDate <= now
                     && c.EndDate   >= now)
            .OrderBy(c => c.EndDate)
            .ToListAsync(ct);

        if (contests.Count == 0)
            return Ok(ApiResponse<List<ContestWidgetDto>>.Ok(new()));

        var contestIds = contests.Select(c => c.Id).ToList();

        // One round-trip: aggregate every leaderboard at once. We over-fetch
        // (no TopX cap) so the viewer's rank can be resolved even when they
        // sit outside the displayed slice.
        var aggregates = await _db.CorporateContestEarnings.AsNoTracking()
            .Where(e => contestIds.Contains(e.ContestId))
            .GroupBy(e => new { e.ContestId, e.BeneficiaryMemberId })
            .Select(g => new
            {
                g.Key.ContestId,
                g.Key.BeneficiaryMemberId,
                TotalPoints = g.Sum(x => x.Points),
                Signups     = g.Select(x => x.SourceMemberId).Distinct().Count()
            })
            .ToListAsync(ct);

        var allMemberIds = aggregates.Select(a => a.BeneficiaryMemberId).Distinct().ToList();
        var profiles = allMemberIds.Count == 0
            ? new Dictionary<string, (string FullName, string Country)>()
            : (await _db.MemberProfiles.AsNoTracking()
                .Where(m => allMemberIds.Contains(m.MemberId))
                .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName, m.Country })
                .ToListAsync(ct))
                .ToDictionary(m => m.MemberId, m => (m.FullName, m.Country));

        var widgets = new List<ContestWidgetDto>(contests.Count);
        foreach (var c in contests)
        {
            // Localized strings — translation row matching the viewer's
            // active culture overrides the contest defaults; missing
            // translation falls back to the contest's English fields.
            var t = c.Translations.FirstOrDefault(
                x => string.Equals(x.LanguageCode, langCode, StringComparison.OrdinalIgnoreCase));

            var ranked = aggregates
                .Where(a => a.ContestId == c.Id)
                .OrderByDescending(a => a.TotalPoints)
                .ThenBy(a => a.BeneficiaryMemberId)
                .ToList();

            var top = ranked.Take(c.TopX)
                .Select((a, i) => new ContestRowDto
                {
                    Rank        = i + 1,
                    MemberId    = a.BeneficiaryMemberId,
                    FullName    = profiles.TryGetValue(a.BeneficiaryMemberId, out var p) ? p.FullName : a.BeneficiaryMemberId,
                    Country     = profiles.TryGetValue(a.BeneficiaryMemberId, out var p2) ? p2.Country : null,
                    TotalPoints = a.TotalPoints,
                    Signups     = a.Signups,
                    IsViewer    = a.BeneficiaryMemberId == memberId
                })
                .ToList();

            // My rank — if I'm in the top list use that index, otherwise scan
            // the full ranked list. Either way the widget reports a number,
            // never null, so the UI can show "Your rank: #N".
            int? myRank = null;
            int  myPoints = 0;
            int  mySignups = 0;
            for (var i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].BeneficiaryMemberId == memberId)
                {
                    myRank    = i + 1;
                    myPoints  = ranked[i].TotalPoints;
                    mySignups = ranked[i].Signups;
                    break;
                }
            }

            widgets.Add(new ContestWidgetDto
            {
                Id                = c.Id,
                Name              = !string.IsNullOrWhiteSpace(t?.Name)        ? t!.Name!        : c.Name,
                Description       = !string.IsNullOrWhiteSpace(t?.Description) ? t!.Description! : c.Description,
                BannerUrl         = !string.IsNullOrWhiteSpace(t?.BannerUrl)   ? t!.BannerUrl!   : c.BannerUrl,
                RulesUrl          = c.RulesUrl,
                StartDate         = c.StartDate,
                EndDate           = c.EndDate,
                TopX              = c.TopX,
                Top               = top,
                MyRank            = myRank,
                MyPoints          = myPoints,
                MySignups         = mySignups,
                Participants      = ranked.Count,
                PointsBoxXPercent = c.PointsBoxXPercent,
                PointsBoxYPercent = c.PointsBoxYPercent
            });
        }

        return Ok(ApiResponse<List<ContestWidgetDto>>.Ok(widgets));
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────
    public class ContestWidgetDto
    {
        public string  Id          { get; set; } = string.Empty;
        public string  Name        { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? BannerUrl   { get; set; }
        public string? RulesUrl    { get; set; }
        public DateTime StartDate  { get; set; }
        public DateTime EndDate    { get; set; }
        public int     TopX        { get; set; }
        public int     Participants { get; set; }
        public List<ContestRowDto> Top { get; set; } = new();
        public int?    MyRank      { get; set; }
        public int     MyPoints    { get; set; }
        public int     MySignups   { get; set; }
        public int     PointsBoxXPercent { get; set; } = 50;
        public int     PointsBoxYPercent { get; set; } = 50;
    }

    public class ContestRowDto
    {
        public int    Rank        { get; set; }
        public string MemberId    { get; set; } = string.Empty;
        public string FullName    { get; set; } = string.Empty;
        public string? Country    { get; set; }
        public int    TotalPoints { get; set; }
        public int    Signups     { get; set; }
        public bool   IsViewer    { get; set; }
    }
}
