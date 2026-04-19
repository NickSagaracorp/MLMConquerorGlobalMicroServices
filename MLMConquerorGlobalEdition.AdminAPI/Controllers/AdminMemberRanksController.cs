using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
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

    /// <summary>PUT /lifetime — set the lifetime rank for a member (admin override for testing).</summary>
    [HttpPut("lifetime")]
    public async Task<IActionResult> SetLifetimeRank(
        string memberId,
        [FromBody] SetLifetimeRankRequest request,
        CancellationToken ct = default)
    {
        var rankDef = await _db.RankDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RankDefinitionId && r.Status == MLMConquerorGlobalEdition.Domain.Entities.Rank.RankDefinitionStatus.Active, ct);

        if (rankDef is null)
            return BadRequest(ApiResponse<object>.Fail("RANK_NOT_FOUND", "Rank definition not found or inactive."));

        var memberExists = await _db.MemberProfiles
            .AsNoTracking()
            .AnyAsync(m => m.MemberId == memberId, ct);

        if (!memberExists)
            return NotFound(ApiResponse<object>.Fail("MEMBER_NOT_FOUND", "Member not found."));

        var previous = await _db.MemberRankHistories
            .AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt)
            .Select(r => (int?)r.RankDefinitionId)
            .FirstOrDefaultAsync(ct);

        var now = DateTime.UtcNow;
        await _db.MemberRankHistories.AddAsync(new MemberRankHistory
        {
            MemberId          = memberId,
            RankDefinitionId  = request.RankDefinitionId,
            PreviousRankId    = previous,
            AchievedAt        = now,
            CreatedBy         = User.Identity?.Name ?? "admin",
            CreationDate      = now,
            LastUpdateDate    = now
        }, ct);

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { message = $"Lifetime rank set to '{rankDef.Name}'.", rankName = rankDef.Name }));
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

    public class RankDefinitionDto
    {
        public int    Id        { get; set; }
        public string Name      { get; set; } = string.Empty;
        public int    SortOrder { get; set; }
    }

    public class SetLifetimeRankRequest
    {
        public int RankDefinitionId { get; set; }
    }
}
