using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.GetProducts;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, Result<IEnumerable<ProductDto>>>
{
    private readonly AppDbContext _db;

    public GetProductsHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<ProductDto>>> Handle(GetProductsQuery query, CancellationToken ct)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto
            {
                Id                 = p.Id,
                Name               = p.Name,
                Description        = p.Description,
                Price              = p.SetupFee > 0 ? p.SetupFee : p.MonthlyFee,
                Sku                = p.OldSystemProductId > 0 ? p.OldSystemProductId.ToString() : null,
                CorporateFee       = p.CorporateFee,
                JoinPageMembership = p.JoinPageMembership
            })
            .ToListAsync(ct);

        return Result<IEnumerable<ProductDto>>.Success(products);
    }
}
