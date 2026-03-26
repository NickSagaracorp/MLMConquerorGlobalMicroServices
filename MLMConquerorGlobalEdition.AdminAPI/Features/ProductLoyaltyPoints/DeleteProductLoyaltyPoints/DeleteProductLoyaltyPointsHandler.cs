using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.DeleteProductLoyaltyPoints;

public class DeleteProductLoyaltyPointsHandler
    : IRequestHandler<DeleteProductLoyaltyPointsCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteProductLoyaltyPointsHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<bool>> Handle(
        DeleteProductLoyaltyPointsCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductLoyaltySettings
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.ProductId == request.ProductId, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("LOYALTY_SETTING_NOT_FOUND", "Product loyalty points setting not found.");

        entity.IsActive = false;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
