using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/countries")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CountriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private const string CacheKey = "countries:all";

    public CountriesController(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive = null,
        [FromQuery] PagedRequest? request = null,
        CancellationToken ct = default)
    {
        request ??= new PagedRequest();
        var query = _db.Countries.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new CountryDto(
                x.Id, x.Iso2, x.Iso3, x.NameEn, x.NameNative,
                x.DefaultLanguageCode, x.FlagEmoji, x.PhoneCode,
                x.IsActive, x.SortOrder))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<CountryDto>>.Ok(new PagedResult<CountryDto>
        {
            Items = items, TotalCount = total,
            Page = request.Page, PageSize = request.PageSize
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.Countries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        return Ok(ApiResponse<CountryDto>.Ok(ToDto(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CountryFormDto dto, CancellationToken ct = default)
    {
        var iso2 = dto.Iso2.Trim().ToUpperInvariant();
        var iso3 = dto.Iso3.Trim().ToUpperInvariant();

        if (await _db.Countries.AnyAsync(x => x.Iso2 == iso2, ct))
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_ISO2", $"ISO2 code '{iso2}' already exists."));

        if (await _db.Countries.AnyAsync(x => x.Iso3 == iso3, ct))
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_ISO3", $"ISO3 code '{iso3}' already exists."));

        var entity = new Country
        {
            Iso2 = iso2,
            Iso3 = iso3,
            NameEn = dto.NameEn.Trim(),
            NameNative = dto.NameNative.Trim(),
            DefaultLanguageCode = dto.DefaultLanguageCode.Trim().ToLowerInvariant(),
            FlagEmoji = dto.FlagEmoji.Trim(),
            PhoneCode = dto.PhoneCode?.Trim(),
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            CreatedBy = User.Identity?.Name ?? "admin",
            CreationDate = DateTime.UtcNow
        };

        await _db.Countries.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            ApiResponse<CountryDto>.Ok(ToDto(entity)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CountryFormDto dto, CancellationToken ct = default)
    {
        var entity = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        var iso2 = dto.Iso2.Trim().ToUpperInvariant();
        var iso3 = dto.Iso3.Trim().ToUpperInvariant();

        if (await _db.Countries.AnyAsync(x => x.Iso2 == iso2 && x.Id != id, ct))
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_ISO2", $"ISO2 code '{iso2}' is used by another country."));

        if (await _db.Countries.AnyAsync(x => x.Iso3 == iso3 && x.Id != id, ct))
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_ISO3", $"ISO3 code '{iso3}' is used by another country."));

        entity.Iso2 = iso2;
        entity.Iso3 = iso3;
        entity.NameEn = dto.NameEn.Trim();
        entity.NameNative = dto.NameNative.Trim();
        entity.DefaultLanguageCode = dto.DefaultLanguageCode.Trim().ToLowerInvariant();
        entity.FlagEmoji = dto.FlagEmoji.Trim();
        entity.PhoneCode = dto.PhoneCode?.Trim();
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return Ok(ApiResponse<CountryDto>.Ok(ToDto(entity)));
    }

    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct = default)
    {
        var entity = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        entity.IsActive = !entity.IsActive;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return Ok(ApiResponse<object>.Ok(new { id, isActive = entity.IsActive }));
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static CountryDto ToDto(Country x) => new(
        x.Id, x.Iso2, x.Iso3, x.NameEn, x.NameNative,
        x.DefaultLanguageCode, x.FlagEmoji, x.PhoneCode,
        x.IsActive, x.SortOrder);

    // ── DTOs ──────────────────────────────────────────────────────────────

    public record CountryDto(
        int Id, string Iso2, string Iso3, string NameEn, string NameNative,
        string DefaultLanguageCode, string FlagEmoji, string? PhoneCode,
        bool IsActive, int SortOrder);

    public record CountryFormDto(
        string Iso2, string Iso3, string NameEn, string NameNative,
        string DefaultLanguageCode, string FlagEmoji, string? PhoneCode,
        bool IsActive, int SortOrder);
}
