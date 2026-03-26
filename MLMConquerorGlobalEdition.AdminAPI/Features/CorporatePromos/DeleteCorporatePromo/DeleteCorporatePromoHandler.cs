using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.DeleteCorporatePromo;

public class DeleteCorporatePromoHandler : IRequestHandler<DeleteCorporatePromoCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteCorporatePromoHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<bool>> Handle(
        DeleteCorporatePromoCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CorporatePromos
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("PROMO_NOT_FOUND", "Corporate promo not found.");

        entity.IsActive = false;
        entity.IsDeleted = true;
        entity.DeletedAt = _dateTime.Now;
        entity.DeletedBy = _currentUser.UserId;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
