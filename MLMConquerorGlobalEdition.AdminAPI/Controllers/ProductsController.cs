using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Products;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IHtmlSanitizerService _sanitizer;

    private static readonly IReadOnlySet<string> _allowedThemes = new HashSet<string>
    {
        "theme-product-guest",
        "theme-product-vip",
        "theme-product-elite",
        "theme-product-turbo"
    };

    public ProductsController(AppDbContext db, IMapper mapper, IHtmlSanitizerService sanitizer)
    {
        _db        = db;
        _mapper    = mapper;
        _sanitizer = sanitizer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var result = new PagedResult<ProductDto>
        {
            Items = _mapper.Map<IEnumerable<ProductDto>>(items),
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Ok(ApiResponse<PagedResult<ProductDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var entity = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<ProductDto>.Fail("PRODUCT_NOT_FOUND", $"Product '{id}' not found."));

        return Ok(ApiResponse<ProductDto>.Ok(_mapper.Map<ProductDto>(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto, CancellationToken ct = default)
    {
        if (dto.ThemeClass is not null && !_allowedThemes.Contains(dto.ThemeClass))
            return BadRequest(ApiResponse<ProductDto>.Fail("INVALID_THEME", $"ThemeClass '{dto.ThemeClass}' is not allowed."));

        var entity = _mapper.Map<Product>(dto);
        entity.Description      = _sanitizer.Sanitize(dto.Description);
        entity.DescriptionPromo = string.IsNullOrWhiteSpace(dto.DescriptionPromo)
            ? null
            : _sanitizer.Sanitize(dto.DescriptionPromo);
        entity.ThemeClass  = dto.ThemeClass;
        entity.CreatedBy   = User.Identity?.Name ?? "admin";

        await _db.Products.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<ProductDto>.Ok(_mapper.Map<ProductDto>(entity)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateProductDto dto, CancellationToken ct = default)
    {
        if (dto.ThemeClass is not null && !_allowedThemes.Contains(dto.ThemeClass))
            return BadRequest(ApiResponse<ProductDto>.Fail("INVALID_THEME", $"ThemeClass '{dto.ThemeClass}' is not allowed."));

        var entity = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<ProductDto>.Fail("PRODUCT_NOT_FOUND", $"Product '{id}' not found."));

        _mapper.Map(dto, entity);
        entity.Description      = _sanitizer.Sanitize(dto.Description);
        entity.DescriptionPromo = string.IsNullOrWhiteSpace(dto.DescriptionPromo)
            ? null
            : _sanitizer.Sanitize(dto.DescriptionPromo);
        entity.ThemeClass    = dto.ThemeClass;
        entity.LastUpdateBy  = User.Identity?.Name ?? "admin";

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<ProductDto>.Ok(_mapper.Map<ProductDto>(entity)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("PRODUCT_NOT_FOUND", $"Product '{id}' not found."));

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.Now;
        entity.DeletedBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Product deleted."));
    }
}
