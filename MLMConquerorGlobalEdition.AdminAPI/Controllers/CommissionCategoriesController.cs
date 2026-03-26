using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/commission-categories")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CommissionCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public CommissionCategoriesController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var items = await _db.CommissionCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<CommissionCategoryDto>>.Ok(_mapper.Map<IEnumerable<CommissionCategoryDto>>(items)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.CommissionCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<CommissionCategoryDto>.Fail("COMMISSION_CATEGORY_NOT_FOUND", $"Commission category '{id}' not found."));

        return Ok(ApiResponse<CommissionCategoryDto>.Ok(_mapper.Map<CommissionCategoryDto>(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommissionCategoryDto dto, CancellationToken ct = default)
    {
        var entity = _mapper.Map<CommissionCategory>(dto);
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.CommissionCategories.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<CommissionCategoryDto>.Ok(_mapper.Map<CommissionCategoryDto>(entity)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCommissionCategoryDto dto, CancellationToken ct = default)
    {
        var entity = await _db.CommissionCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<CommissionCategoryDto>.Fail("COMMISSION_CATEGORY_NOT_FOUND", $"Commission category '{id}' not found."));

        _mapper.Map(dto, entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<CommissionCategoryDto>.Ok(_mapper.Map<CommissionCategoryDto>(entity)));
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
