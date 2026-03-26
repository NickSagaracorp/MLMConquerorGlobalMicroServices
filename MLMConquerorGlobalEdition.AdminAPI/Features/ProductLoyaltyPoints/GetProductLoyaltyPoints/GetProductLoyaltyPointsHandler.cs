using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.GetProductLoyaltyPoints;

public class GetProductLoyaltyPointsHandler
    : IRequestHandler<GetProductLoyaltyPointsQuery, Result<IEnumerable<ProductLoyaltyPointsDto>>>
{
    private readonly AppDbContext _db;

    public GetProductLoyaltyPointsHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<ProductLoyaltyPointsDto>>> Handle(
        GetProductLoyaltyPointsQuery request, CancellationToken cancellationToken)
    {
        var items = await _db.ProductLoyaltySettings
            .AsNoTracking()
            .Where(x => x.ProductId == request.ProductId)
            .OrderBy(x => x.Id)
            .Select(x => new ProductLoyaltyPointsDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                PointsPerUnit = x.PointsPerUnit,
                RequiredSuccessfulPayments = x.RequiredSuccessfulPayments,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Result<IEnumerable<ProductLoyaltyPointsDto>>.Success(items);
    }
}
