using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.DeleteProductCommission;

public class DeleteProductCommissionHandler
    : IRequestHandler<DeleteProductCommissionCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteProductCommissionHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(
        DeleteProductCommissionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductCommissions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("PRODUCT_COMMISSION_NOT_FOUND", "Product commission not found.");

        _db.ProductCommissions.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
