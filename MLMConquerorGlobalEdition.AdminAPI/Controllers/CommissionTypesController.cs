using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.Mappings;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/commission-types")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CommissionTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public CommissionTypesController(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.CommissionTypes.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var result = new PagedResult<CommissionTypeDto>
        {
            Items = items.Select(x => x.ToDto()),
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Ok(ApiResponse<PagedResult<CommissionTypeDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.CommissionTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<CommissionTypeDto>.Fail("COMMISSION_TYPE_NOT_FOUND", $"Commission type '{id}' not found."));

        return Ok(ApiResponse<CommissionTypeDto>.Ok(entity.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommissionTypeDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToNewEntity();
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.CommissionTypes.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.CommissionTypes, ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<CommissionTypeDto>.Ok(entity.ToDto()));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCommissionTypeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.CommissionTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<CommissionTypeDto>.Fail("COMMISSION_TYPE_NOT_FOUND", $"Commission type '{id}' not found."));

        dto.ApplyTo(entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.CommissionTypes, ct);
        return Ok(ApiResponse<CommissionTypeDto>.Ok(entity.ToDto()));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.CommissionTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("COMMISSION_TYPE_NOT_FOUND", $"Commission type '{id}' not found."));

        entity.IsActive = false;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.CommissionTypes, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Commission type deactivated."));
    }
}
