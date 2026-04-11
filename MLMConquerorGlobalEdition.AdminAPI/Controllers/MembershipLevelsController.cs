using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.MembershipLevels;
using MLMConquerorGlobalEdition.AdminAPI.Mappings;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/membership-levels")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class MembershipLevelsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public MembershipLevelsController(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var items = await _db.MembershipLevels.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<MembershipLevelDto>>.Ok(items.Select(x => x.ToDto())));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.MembershipLevels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<MembershipLevelDto>.Fail("MEMBERSHIP_LEVEL_NOT_FOUND", $"Membership level '{id}' not found."));

        return Ok(ApiResponse<MembershipLevelDto>.Ok(entity.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMembershipLevelDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToNewEntity();
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.MembershipLevels.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MembershipLevels, ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<MembershipLevelDto>.Ok(entity.ToDto()));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMembershipLevelDto dto, CancellationToken ct = default)
    {
        var entity = await _db.MembershipLevels.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<MembershipLevelDto>.Fail("MEMBERSHIP_LEVEL_NOT_FOUND", $"Membership level '{id}' not found."));

        dto.ApplyTo(entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MembershipLevels, ct);
        return Ok(ApiResponse<MembershipLevelDto>.Ok(entity.ToDto()));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.MembershipLevels.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("MEMBERSHIP_LEVEL_NOT_FOUND", $"Membership level '{id}' not found."));

        entity.IsActive = false;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MembershipLevels, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Membership level deactivated."));
    }
}
