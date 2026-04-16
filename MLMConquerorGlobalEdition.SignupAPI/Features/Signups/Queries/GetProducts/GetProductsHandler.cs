using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetProducts;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, Result<IEnumerable<ProductDto>>>
{
    private readonly AppDbContext  _db;
    private readonly ICacheService _cache;

    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public GetProductsHandler(AppDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<Result<IEnumerable<ProductDto>>> Handle(GetProductsQuery query, CancellationToken ct)
    {
        var iso2 = query.CountryIso2?.Trim().ToUpperInvariant();
        var cacheKey = string.IsNullOrEmpty(iso2)
            ? "signup:products:all"
            : $"signup:products:{iso2}";

        List<ProductDto>? cached = null;
        try
        {
            cached = await _cache.GetAsync<List<ProductDto>>(cacheKey, ct);
        }
        catch
        {
            // Cache unavailable — fall through to database
        }

        if (cached is not null)
            return Result<IEnumerable<ProductDto>>.Success(cached);

        List<ProductDto> products;

        if (!string.IsNullOrEmpty(iso2))
        {
            // Resolve country → check for explicit product mappings
            var countryId = await _db.Countries
                .AsNoTracking()
                .Where(c => c.Iso2 == iso2)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);

            if (countryId is not null)
            {
                var mappedProductIds = await _db.CountryProducts
                    .AsNoTracking()
                    .Where(cp => cp.CountryId == countryId && cp.IsActive)
                    .Select(cp => cp.ProductId)
                    .ToListAsync(ct);

                if (mappedProductIds.Count > 0)
                {
                    // Return only the products in the mapping
                    products = await _db.Products
                        .AsNoTracking()
                        .Where(p => mappedProductIds.Contains(p.Id) && p.IsActive && !p.IsDeleted)
                        .OrderBy(p => p.Name)
                        .Select(p => new ProductDto
                        {
                            Id                 = p.Id,
                            Name               = p.Name,
                            Description        = p.Description,
                            ImageUrl           = p.ImageUrl,
                            ThemeClass         = p.ThemeClass,
                            Price              = p.SetupFee > 0 ? p.SetupFee : p.MonthlyFee,
                            Sku                = p.OldSystemProductId > 0 ? p.OldSystemProductId.ToString() : null,
                            CorporateFee       = p.CorporateFee,
                            JoinPageMembership = p.JoinPageMembership,
                            MembershipLevelId  = p.MembershipLevelId
                        })
                        .ToListAsync(ct);

                    try { await _cache.SetAsync(cacheKey, products, Ttl, ct); } catch { /* cache write is best-effort */ }
                    return Result<IEnumerable<ProductDto>>.Success(products);
                }
                // Country exists but has no mappings → fall through to default list
            }
        }

        // Default: all active products shown on the join page (membership plans + corporate fee)
        products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted && (p.JoinPageMembership || p.CorporateFee))
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto
            {
                Id                 = p.Id,
                Name               = p.Name,
                Description        = p.Description,
                ImageUrl           = p.ImageUrl,
                ThemeClass         = p.ThemeClass,
                Price              = p.SetupFee > 0 ? p.SetupFee : p.MonthlyFee,
                Sku                = p.OldSystemProductId > 0 ? p.OldSystemProductId.ToString() : null,
                CorporateFee       = p.CorporateFee,
                JoinPageMembership = p.JoinPageMembership,
                MembershipLevelId  = p.MembershipLevelId
            })
            .ToListAsync(ct);

        try { await _cache.SetAsync(cacheKey, products, Ttl, ct); } catch { /* cache write is best-effort */ }
        return Result<IEnumerable<ProductDto>>.Success(products);
    }
}
