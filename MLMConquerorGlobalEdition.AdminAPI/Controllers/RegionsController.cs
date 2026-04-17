using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/regions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class RegionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private const string CacheKey = "regions:all";

    public RegionsController(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    // ── Regions CRUD ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _db.Regions.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var items = await query
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new RegionDto(
                x.Id, x.Name, x.Code, x.Description,
                x.IsActive, x.SortOrder,
                x.Gateways.Where(g => g.IsActive)
                          .OrderBy(g => g.Priority)
                          .Select(g => new RegionGatewayDto(g.Id, g.GatewayType, g.GatewayType.ToString(), g.Priority, g.IsActive))
                          .ToList(),
                x.Countries.Count(c => c.IsActive)))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<RegionDto>>.Ok(items));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.Regions
            .AsNoTracking()
            .Include(x => x.Gateways)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("REGION_NOT_FOUND", $"Region '{id}' not found."));

        return Ok(ApiResponse<RegionDto>.Ok(ToDto(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RegionFormDto dto, CancellationToken ct = default)
    {
        var code = dto.Code.Trim().ToUpperInvariant();

        if (await _db.Regions.AnyAsync(x => x.Code == code, ct))
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_CODE", $"Region code '{code}' already exists."));

        var entity = new Region
        {
            Name        = dto.Name.Trim(),
            Code        = code,
            Description = dto.Description?.Trim(),
            IsActive    = dto.IsActive,
            SortOrder   = dto.SortOrder,
            CreatedBy   = User.Identity?.Name ?? "admin",
            CreationDate = DateTime.UtcNow
        };

        await _db.Regions.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            ApiResponse<RegionDto>.Ok(ToDto(entity)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] RegionFormDto dto, CancellationToken ct = default)
    {
        var entity = await _db.Regions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("REGION_NOT_FOUND", $"Region '{id}' not found."));

        var code = dto.Code.Trim().ToUpperInvariant();

        if (await _db.Regions.AnyAsync(x => x.Code == code && x.Id != id, ct))
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_CODE", $"Region code '{code}' is used by another region."));

        entity.Name        = dto.Name.Trim();
        entity.Code        = code;
        entity.Description = dto.Description?.Trim();
        entity.IsActive    = dto.IsActive;
        entity.SortOrder   = dto.SortOrder;
        entity.LastUpdateBy   = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return Ok(ApiResponse<RegionDto>.Ok(ToDto(entity)));
    }

    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct = default)
    {
        var entity = await _db.Regions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("REGION_NOT_FOUND", $"Region '{id}' not found."));

        entity.IsActive       = !entity.IsActive;
        entity.LastUpdateBy   = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return Ok(ApiResponse<object>.Ok(new { id, isActive = entity.IsActive }));
    }

    // ── Region → Gateways ─────────────────────────────────────────────────

    [HttpGet("{id:int}/gateways")]
    public async Task<IActionResult> GetGateways(int id, CancellationToken ct = default)
    {
        if (!await _db.Regions.AnyAsync(x => x.Id == id, ct))
            return NotFound(ApiResponse<object>.Fail("REGION_NOT_FOUND", $"Region '{id}' not found."));

        var gateways = await _db.RegionGateways
            .AsNoTracking()
            .Where(g => g.RegionId == id)
            .OrderBy(g => g.Priority)
            .Select(g => new RegionGatewayDto(g.Id, g.GatewayType, g.GatewayType.ToString(), g.Priority, g.IsActive))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<RegionGatewayDto>>.Ok(gateways));
    }

    /// <summary>
    /// PUT /api/v1/admin/regions/{id}/gateways
    /// Bulk-replaces all gateways for a region. Send the complete desired list.
    /// </summary>
    [HttpPut("{id:int}/gateways")]
    public async Task<IActionResult> SetGateways(
        int id, [FromBody] List<GatewayEntryRequest> request, CancellationToken ct = default)
    {
        var region = await _db.Regions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (region is null)
            return NotFound(ApiResponse<object>.Fail("REGION_NOT_FOUND", $"Region '{id}' not found."));

        // Remove all existing gateways
        var existing = await _db.RegionGateways.Where(g => g.RegionId == id).ToListAsync(ct);
        _db.RegionGateways.RemoveRange(existing);

        // Add the new set (de-duplicate by GatewayType)
        var now   = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";
        var seen  = new HashSet<WalletType>();
        var newGateways = new List<RegionGateway>();

        foreach (var entry in request.OrderBy(x => x.Priority))
        {
            if (!seen.Add(entry.GatewayType)) continue; // skip duplicates
            newGateways.Add(new RegionGateway
            {
                RegionId     = id,
                GatewayType  = entry.GatewayType,
                Priority     = entry.Priority,
                IsActive     = entry.IsActive,
                CreatedBy    = actor,
                CreationDate = now
            });
        }

        await _db.RegionGateways.AddRangeAsync(newGateways, ct);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return Ok(ApiResponse<object>.Ok(new { regionId = id, gatewayCount = newGateways.Count }));
    }

    // ── Region → Countries ────────────────────────────────────────────────

    [HttpGet("{id:int}/countries")]
    public async Task<IActionResult> GetCountries(int id, CancellationToken ct = default)
    {
        if (!await _db.Regions.AnyAsync(x => x.Id == id, ct))
            return NotFound(ApiResponse<object>.Fail("REGION_NOT_FOUND", $"Region '{id}' not found."));

        var countries = await _db.Countries
            .AsNoTracking()
            .Where(c => c.RegionId == id)
            .OrderBy(c => c.NameEn)
            .Select(c => new RegionCountryDto(c.Id, c.Iso2, c.NameEn, c.FlagEmoji, c.IsActive))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<RegionCountryDto>>.Ok(countries));
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static RegionDto ToDto(Region x) => new(
        x.Id, x.Name, x.Code, x.Description, x.IsActive, x.SortOrder,
        x.Gateways.Where(g => g.IsActive)
                  .OrderBy(g => g.Priority)
                  .Select(g => new RegionGatewayDto(g.Id, g.GatewayType, g.GatewayType.ToString(), g.Priority, g.IsActive))
                  .ToList(),
        x.Countries.Count(c => c.IsActive));

    // ── DTOs ──────────────────────────────────────────────────────────────

    public record RegionDto(
        int Id, string Name, string Code, string? Description,
        bool IsActive, int SortOrder,
        List<RegionGatewayDto> Gateways,
        int ActiveCountryCount);

    public record RegionGatewayDto(
        int Id, WalletType GatewayType, string GatewayName, int Priority, bool IsActive);

    public record RegionCountryDto(
        int Id, string Iso2, string NameEn, string FlagEmoji, bool IsActive);

    public record RegionFormDto(
        string Name, string Code, string? Description, bool IsActive, int SortOrder);

    public record GatewayEntryRequest(WalletType GatewayType, int Priority, bool IsActive);
}
