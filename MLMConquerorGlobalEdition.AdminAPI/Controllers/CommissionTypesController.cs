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
[Route("api/v1/admin/commission-types")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CommissionTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public CommissionTypesController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
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
            Items = _mapper.Map<IEnumerable<CommissionTypeDto>>(items),
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

        return Ok(ApiResponse<CommissionTypeDto>.Ok(_mapper.Map<CommissionTypeDto>(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommissionTypeDto dto, CancellationToken ct = default)
    {
        var entity = _mapper.Map<CommissionType>(dto);
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.CommissionTypes.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<CommissionTypeDto>.Ok(_mapper.Map<CommissionTypeDto>(entity)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCommissionTypeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.CommissionTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<CommissionTypeDto>.Fail("COMMISSION_TYPE_NOT_FOUND", $"Commission type '{id}' not found."));

        _mapper.Map(dto, entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<CommissionTypeDto>.Ok(_mapper.Map<CommissionTypeDto>(entity)));
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
        return Ok(ApiResponse<object>.Ok(new { }, "Commission type deactivated."));
    }
}
