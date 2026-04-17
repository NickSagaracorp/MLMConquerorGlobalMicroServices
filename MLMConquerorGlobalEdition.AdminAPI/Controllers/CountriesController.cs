using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

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
        [FromQuery] int? regionId = null,
        [FromQuery] PagedRequest? request = null,
        CancellationToken ct = default)
    {
        request ??= new PagedRequest();
        var query = _db.Countries.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        if (regionId.HasValue)
            query = regionId.Value == 0
                ? query.Where(x => x.RegionId == null)          // unassigned
                : query.Where(x => x.RegionId == regionId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new CountryDto(
                x.Id, x.Iso2, x.Iso3, x.NameEn, x.NameNative,
                x.DefaultLanguageCode, x.FlagEmoji, x.PhoneCode,
                x.IsActive, x.SortOrder, x.RegionId,
                x.Region != null ? x.Region.Name : null))
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
            .Include(x => x.Region)
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

    // ── Country → Product Mappings ─────────────────────────────────────────

    private const string ProductsCacheKeyPrefix = "signup:products:";

    /// <summary>GET /api/v1/admin/countries/{id}/products — list active mappings.</summary>
    [HttpGet("{id:int}/products")]
    public async Task<IActionResult> GetProducts(int id, CancellationToken ct = default)
    {
        var countryExists = await _db.Countries.AnyAsync(x => x.Id == id, ct);
        if (!countryExists)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        var raw = await _db.CountryProducts
            .AsNoTracking()
            .Where(cp => cp.CountryId == id)
            .Join(_db.Products.AsNoTracking(),
                cp => cp.ProductId,
                p  => p.Id,
                (cp, p) => new { cp.Id, ProductId = cp.ProductId, ProductName = p.Name, cp.IsActive })
            .OrderBy(x => x.ProductName)
            .ToListAsync(ct);

        var mappings = raw
            .Select(x => new CountryProductDto(x.Id, x.ProductId, x.ProductName, x.IsActive))
            .ToList();

        return Ok(ApiResponse<IEnumerable<CountryProductDto>>.Ok(mappings));
    }

    /// <summary>
    /// PUT /api/v1/admin/countries/{id}/products — bulk replace all product mappings for a country.
    /// Sends the complete list of productIds that should be active; previous mappings not in the list are removed.
    /// </summary>
    [HttpPut("{id:int}/products")]
    public async Task<IActionResult> SetProducts(
        int id, [FromBody] CountryProductsBulkRequest request, CancellationToken ct = default)
    {
        var country = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (country is null)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        var productIds = request.ProductIds.Distinct().ToList();

        // Validate all requested products exist
        var validIds = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsActive && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var invalid = productIds.Except(validIds).ToList();
        if (invalid.Count > 0)
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_PRODUCTS", $"Products not found or inactive: {string.Join(", ", invalid)}."));

        // Remove existing mappings for this country
        var existing = await _db.CountryProducts.Where(cp => cp.CountryId == id).ToListAsync(ct);
        _db.CountryProducts.RemoveRange(existing);

        // Add the new set
        var now   = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";
        var newMappings = productIds.Select(pid => new CountryProduct
        {
            CountryId    = id,
            ProductId    = pid,
            IsActive     = true,
            CreatedBy    = actor,
            CreationDate = now
        }).ToList();

        await _db.CountryProducts.AddRangeAsync(newMappings, ct);
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync($"{ProductsCacheKeyPrefix}{country.Iso2.ToLowerInvariant()}", ct);

        return Ok(ApiResponse<object>.Ok(new { countryId = id, productCount = newMappings.Count }));
    }

    /// <summary>POST /api/v1/admin/countries/{id}/products/{productId} — add a single mapping.</summary>
    [HttpPost("{id:int}/products/{productId}")]
    public async Task<IActionResult> AddProduct(
        int id, string productId, CancellationToken ct = default)
    {
        var country = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (country is null)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        var productExists = await _db.Products
            .AnyAsync(p => p.Id == productId && p.IsActive && !p.IsDeleted, ct);
        if (!productExists)
            return NotFound(ApiResponse<object>.Fail("PRODUCT_NOT_FOUND", $"Product '{productId}' not found or inactive."));

        var alreadyMapped = await _db.CountryProducts
            .AnyAsync(cp => cp.CountryId == id && cp.ProductId == productId, ct);
        if (alreadyMapped)
            return Conflict(ApiResponse<object>.Fail(
                "MAPPING_EXISTS", $"Product '{productId}' is already mapped to this country."));

        var mapping = new CountryProduct
        {
            CountryId    = id,
            ProductId    = productId,
            IsActive     = true,
            CreatedBy    = User.Identity?.Name ?? "admin",
            CreationDate = DateTime.UtcNow
        };

        await _db.CountryProducts.AddAsync(mapping, ct);
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync($"{ProductsCacheKeyPrefix}{country.Iso2.ToLowerInvariant()}", ct);

        return StatusCode(201, ApiResponse<object>.Ok(new { id = mapping.Id, countryId = id, productId }));
    }

    /// <summary>DELETE /api/v1/admin/countries/{id}/products/{productId} — remove a mapping.</summary>
    [HttpDelete("{id:int}/products/{productId}")]
    public async Task<IActionResult> RemoveProduct(
        int id, string productId, CancellationToken ct = default)
    {
        var country = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (country is null)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        var mapping = await _db.CountryProducts
            .FirstOrDefaultAsync(cp => cp.CountryId == id && cp.ProductId == productId, ct);
        if (mapping is null)
            return NotFound(ApiResponse<object>.Fail(
                "MAPPING_NOT_FOUND", $"No mapping found for product '{productId}' in country '{id}'."));

        _db.CountryProducts.Remove(mapping);
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync($"{ProductsCacheKeyPrefix}{country.Iso2.ToLowerInvariant()}", ct);

        return Ok(ApiResponse<object>.Ok(new { countryId = id, productId, removed = true }));
    }

    // ── Country → Region assignment ───────────────────────────────────────

    [HttpPatch("{id:int}/region")]
    public async Task<IActionResult> AssignRegion(
        int id, [FromBody] AssignRegionRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"Country '{id}' not found."));

        if (request.RegionId.HasValue)
        {
            var regionExists = await _db.Regions.AnyAsync(x => x.Id == request.RegionId.Value, ct);
            if (!regionExists)
                return NotFound(ApiResponse<object>.Fail("REGION_NOT_FOUND", $"Region '{request.RegionId}' not found."));
        }

        entity.RegionId       = request.RegionId;
        entity.LastUpdateBy   = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return Ok(ApiResponse<object>.Ok(new { id, regionId = entity.RegionId }));
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static CountryDto ToDto(Country x) => new(
        x.Id, x.Iso2, x.Iso3, x.NameEn, x.NameNative,
        x.DefaultLanguageCode, x.FlagEmoji, x.PhoneCode,
        x.IsActive, x.SortOrder, x.RegionId, x.Region?.Name);

    // ── DTOs ──────────────────────────────────────────────────────────────

    public record CountryDto(
        int Id, string Iso2, string Iso3, string NameEn, string NameNative,
        string DefaultLanguageCode, string FlagEmoji, string? PhoneCode,
        bool IsActive, int SortOrder, int? RegionId, string? RegionName);

    public record CountryFormDto(
        string Iso2, string Iso3, string NameEn, string NameNative,
        string DefaultLanguageCode, string FlagEmoji, string? PhoneCode,
        bool IsActive, int SortOrder);

    public record AssignRegionRequest(int? RegionId);

    public record CountryProductDto(int MappingId, string ProductId, string ProductName, bool IsActive);

    public record CountryProductsBulkRequest(IEnumerable<string> ProductIds);
}
