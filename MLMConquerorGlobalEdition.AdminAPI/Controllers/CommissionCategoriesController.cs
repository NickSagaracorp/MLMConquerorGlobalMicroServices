using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.Mappings;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/commission-categories")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CommissionCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CommissionCategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var items = await _db.CommissionCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<CommissionCategoryDto>>.Ok(items.Select(x => x.ToDto())));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.CommissionCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<CommissionCategoryDto>.Fail("COMMISSION_CATEGORY_NOT_FOUND", $"Commission category '{id}' not found."));

        return Ok(ApiResponse<CommissionCategoryDto>.Ok(entity.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommissionCategoryDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToNewEntity();
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.CommissionCategories.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<CommissionCategoryDto>.Ok(entity.ToDto()));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCommissionCategoryDto dto, CancellationToken ct = default)
    {
        var entity = await _db.CommissionCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<CommissionCategoryDto>.Fail("COMMISSION_CATEGORY_NOT_FOUND", $"Commission category '{id}' not found."));

        dto.ApplyTo(entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<CommissionCategoryDto>.Ok(entity.ToDto()));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.CommissionCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("COMMISSION_CATEGORY_NOT_FOUND", $"Commission category '{id}' not found."));

        entity.IsActive = false;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Commission category deactivated."));
    }
}
