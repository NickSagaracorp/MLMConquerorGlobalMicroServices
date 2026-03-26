using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/ticket-categories")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager,IT")]
public class AdminTicketCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminTicketCategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var items = await _db.TicketCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new TicketCategoryDto(x.Id, x.Name, x.IsActive, x.CreationDate))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<TicketCategoryDto>>.Ok(items));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TicketCategoryFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Name is required."));

        var entity = new TicketCategory
        {
            Name = dto.Name.Trim(),
            IsActive = dto.IsActive,
            CreatedBy = User.Identity?.Name ?? "admin",
            CreationDate = DateTime.UtcNow
        };

        await _db.TicketCategories.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<TicketCategoryDto>.Ok(
            new TicketCategoryDto(entity.Id, entity.Name, entity.IsActive, entity.CreationDate)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TicketCategoryFormDto dto, CancellationToken ct = default)
    {
        var entity = await _db.TicketCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"Ticket category {id} not found."));

        entity.Name = dto.Name.Trim();
        entity.IsActive = dto.IsActive;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<TicketCategoryDto>.Ok(
            new TicketCategoryDto(entity.Id, entity.Name, entity.IsActive, entity.CreationDate)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.TicketCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"Ticket category {id} not found."));

        _db.TicketCategories.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    public record TicketCategoryDto(int Id, string Name, bool IsActive, DateTime CreationDate);
    public record TicketCategoryFormDto(string Name, bool IsActive);
}
