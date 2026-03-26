using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/rank-definitions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class RankDefinitionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public RankDefinitionsController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var items = await _db.RankDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<RankDefinitionDto>>.Ok(_mapper.Map<IEnumerable<RankDefinitionDto>>(items)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.RankDefinitions.AsNoTracking()
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<RankDefinitionDto>.Fail("RANK_NOT_FOUND", $"Rank definition '{id}' not found."));

        return Ok(ApiResponse<RankDefinitionDto>.Ok(_mapper.Map<RankDefinitionDto>(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRankDefinitionDto dto, CancellationToken ct = default)
    {
        var entity = _mapper.Map<RankDefinition>(dto);
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.RankDefinitions.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<RankDefinitionDto>.Ok(_mapper.Map<RankDefinitionDto>(entity)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRankDefinitionDto dto, CancellationToken ct = default)
    {
        var entity = await _db.RankDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<RankDefinitionDto>.Fail("RANK_NOT_FOUND", $"Rank definition '{id}' not found."));

        _mapper.Map(dto, entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<RankDefinitionDto>.Ok(_mapper.Map<RankDefinitionDto>(entity)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.RankDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("RANK_NOT_FOUND", $"Rank definition '{id}' not found."));

        entity.Status = RankDefinitionStatus.Inactive;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Rank definition deactivated."));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var ranks = await _db.RankDefinitions.AsNoTracking()
            .Where(r => r.Status == RankDefinitionStatus.Active)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        // Get latest rank per member (most recent MemberRankHistory entry)
        var latestRanks = await _db.MemberRankHistories.AsNoTracking()
            .GroupBy(r => r.MemberId)
            .Select(g => g.OrderByDescending(r => r.AchievedAt).First())
            .ToListAsync(ct);

        var totalMembers = latestRanks.Count;

        var rows = ranks.Select(r =>
        {
            var count = latestRanks.Count(lr => lr.RankDefinitionId == r.Id);
            var pct = totalMembers > 0 ? Math.Round((decimal)count / totalMembers * 100, 2) : 0;
            return new { RankId = r.Id, RankName = r.Name, RankCode = (string?)null, MemberCount = count, Percentage = pct };
        }).ToList<object>();

        var highestRank = ranks
            .OrderByDescending(r => r.SortOrder)
            .FirstOrDefault(r => latestRanks.Any(lr => lr.RankDefinitionId == r.Id));

        // Unranked: members that have no rank history at all
        var rankedMemberIds = latestRanks.Select(r => r.MemberId).ToHashSet();
        var totalActiveMembers = await _db.MemberProfiles.AsNoTracking().CountAsync(ct);
        var unrankedCount = Math.Max(0, totalActiveMembers - rankedMemberIds.Count);

        var stats = new
        {
            TotalMembers = totalMembers,
            TotalRanks = ranks.Count,
            HighestRankName = highestRank?.Name,
            UnrankedCount = unrankedCount
        };

        return Ok(ApiResponse<object>.Ok(new { Stats = stats, Rows = rows }));
    }

}
