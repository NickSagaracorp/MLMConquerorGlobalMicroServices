using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Rank data for a specific member — used by Admin Member Profile Dual Team / Residuals tab.
/// Routes: /api/v1/admin/members/{memberId}/ranks/*
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/ranks")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberRanksController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminMemberRanksController(AppDbContext db) => _db = db;

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(string memberId, CancellationToken ct = default)
    {
        // Current rank — latest history entry
        var currentHistory = await _db.MemberRankHistories.AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .Include(r => r.RankDefinition)
            .ThenInclude(d => d!.Requirements)
            .OrderByDescending(r => r.AchievedAt)
            .FirstOrDefaultAsync(ct);

        var currentDef = currentHistory?.RankDefinition;
        var currentReq = currentDef?.Requirements.FirstOrDefault();

        // All ranks ordered by SortOrder to find next
        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var nextDef = currentDef is not null
            ? allRanks.FirstOrDefault(r => r.SortOrder > currentDef.SortOrder)
            : allRanks.FirstOrDefault();

        var nextReq = nextDef?.Requirements.FirstOrDefault();

        var dto = new RankDashboardDto
        {
            CurrentRankName             = currentDef?.Name ?? "—",
            NextRankName                = nextDef?.Name   ?? "—",
            CurrentRankDualTeamPoints   = currentReq?.TeamPoints       ?? 0,
            CurrentRankEnrollmentPoints = currentReq?.EnrollmentTeam   ?? 0,
            NextRankDualTeamPoints      = nextReq?.TeamPoints          ?? 0,
            NextRankEnrollmentPoints    = nextReq?.EnrollmentTeam      ?? 0
        };

        return Ok(ApiResponse<RankDashboardDto>.Ok(dto));
    }

    public class RankDashboardDto
    {
        public string CurrentRankName             { get; set; } = string.Empty;
        public string NextRankName                { get; set; } = string.Empty;
        public int    CurrentRankDualTeamPoints   { get; set; }
        public int    CurrentRankEnrollmentPoints { get; set; }
        public int    NextRankDualTeamPoints      { get; set; }
        public int    NextRankEnrollmentPoints    { get; set; }
    }
}
