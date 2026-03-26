using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.CreateProductLoyaltyPoints;

public class CreateProductLoyaltyPointsHandler
    : IRequestHandler<CreateProductLoyaltyPointsCommand, Result<int>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateProductLoyaltyPointsHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<int>> Handle(
        CreateProductLoyaltyPointsCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new ProductLoyaltyPointsSetting
        {
            ProductId = request.ProductId,
            PointsPerUnit = req.PointsPerUnit,
            RequiredSuccessfulPayments = req.RequiredSuccessfulPayments,
            IsActive = true,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.ProductLoyaltySettings.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
