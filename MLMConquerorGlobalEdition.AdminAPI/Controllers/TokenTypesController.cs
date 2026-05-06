using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;
using MLMConquerorGlobalEdition.AdminAPI.Mappings;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/token-types")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class TokenTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    public TokenTypesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var items = await _db.TokenTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<TokenTypeDto>>.Ok(items.Select(x => x.ToDto())));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.TokenTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<TokenTypeDto>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        return Ok(ApiResponse<TokenTypeDto>.Ok(entity.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTokenTypeDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToNewEntity();
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.TokenTypes.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<TokenTypeDto>.Ok(entity.ToDto()));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTokenTypeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.TokenTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<TokenTypeDto>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        dto.ApplyTo(entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<TokenTypeDto>.Ok(entity.ToDto()));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.TokenTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        entity.IsActive = false;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Token type deactivated."));
    }

    // ─── Product associations ────────────────────────────────────────────

    /// <summary>Returns all product links for a token type, with product names resolved.</summary>
    [HttpGet("{id:int}/products")]
    public async Task<IActionResult> GetProducts(int id, CancellationToken ct = default)
    {
        var exists = await _db.TokenTypes.AnyAsync(x => x.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<IEnumerable<TokenTypeProductDto>>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        var rows = await _db.TokenTypeProducts
            .AsNoTracking()
            .Where(x => x.TokenTypeId == id)
            .Join(_db.Products,
                ttp => ttp.ProductId,
                p   => p.Id,
                (ttp, p) => new TokenTypeProductDto
                {
                    Id              = ttp.Id,
                    TokenTypeId     = ttp.TokenTypeId,
                    ProductId       = ttp.ProductId,
                    ProductName     = p.Name,
                    Role            = ttp.Role,
                    QuantityGranted = ttp.QuantityGranted
                })
            .OrderBy(x => x.Role)
            .ThenBy(x => x.ProductName)
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<TokenTypeProductDto>>.Ok(rows));
    }

    /// <summary>
    /// Replaces the entire set of product links for a token type with the payload contents.
    /// - Granted: any number of products (0..N)
    /// - UpgradeFrom / UpgradeTo: 0 or 1 each, both required when token Category = Upgrade
    /// </summary>
    [HttpPut("{id:int}/products")]
    public async Task<IActionResult> SetProducts(int id, [FromBody] TokenTypeProductsPayloadDto payload, CancellationToken ct = default)
    {
        var token = await _db.TokenTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (token is null)
            return NotFound(ApiResponse<object>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        // Sanity checks for Upgrade tokens
        if (token.Category == TokenCategory.Upgrade)
        {
            if (string.IsNullOrWhiteSpace(payload.UpgradeFromProductId) || string.IsNullOrWhiteSpace(payload.UpgradeToProductId))
                return BadRequest(ApiResponse<object>.Fail("UPGRADE_PATH_REQUIRED",
                    "Upgrade tokens must define both UpgradeFromProductId and UpgradeToProductId."));

            if (string.Equals(payload.UpgradeFromProductId, payload.UpgradeToProductId, StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<object>.Fail("UPGRADE_PATH_INVALID",
                    "UpgradeFromProductId and UpgradeToProductId must be different products."));
        }

        // Validate referenced products exist
        var referencedIds = payload.GrantedProductIds
            .Concat(new[] { payload.UpgradeFromProductId, payload.UpgradeToProductId })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct()
            .ToList();

        if (referencedIds.Count > 0)
        {
            var foundIds = await _db.Products
                .Where(p => referencedIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct);

            var missing = referencedIds.Except(foundIds).ToList();
            if (missing.Count > 0)
                return BadRequest(ApiResponse<object>.Fail("PRODUCT_NOT_FOUND",
                    $"Unknown product id(s): {string.Join(", ", missing)}"));
        }

        var existing = await _db.TokenTypeProducts.Where(x => x.TokenTypeId == id).ToListAsync(ct);
        _db.TokenTypeProducts.RemoveRange(existing);

        var now    = DateTime.UtcNow;
        var actor  = User.Identity?.Name ?? "admin";
        var rows   = new List<TokenTypeProduct>();

        foreach (var pid in payload.GrantedProductIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new TokenTypeProduct
            {
                TokenTypeId     = id,
                ProductId       = pid,
                Role            = TokenProductRole.Granted,
                QuantityGranted = 1,
                CreatedBy       = actor,
                CreationDate    = now
            });
        }

        if (!string.IsNullOrWhiteSpace(payload.UpgradeFromProductId))
        {
            rows.Add(new TokenTypeProduct
            {
                TokenTypeId     = id,
                ProductId       = payload.UpgradeFromProductId,
                Role            = TokenProductRole.UpgradeFrom,
                QuantityGranted = 0,
                CreatedBy       = actor,
                CreationDate    = now
            });
        }

        if (!string.IsNullOrWhiteSpace(payload.UpgradeToProductId))
        {
            rows.Add(new TokenTypeProduct
            {
                TokenTypeId     = id,
                ProductId       = payload.UpgradeToProductId,
                Role            = TokenProductRole.UpgradeTo,
                QuantityGranted = 1,
                CreatedBy       = actor,
                CreationDate    = now
            });
        }

        if (rows.Count > 0)
            await _db.TokenTypeProducts.AddRangeAsync(rows, ct);

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { count = rows.Count }, "Token product links updated."));
    }
}
