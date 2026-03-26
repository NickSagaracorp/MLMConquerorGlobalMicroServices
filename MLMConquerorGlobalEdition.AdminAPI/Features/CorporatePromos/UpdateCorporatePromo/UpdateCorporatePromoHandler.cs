using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.UpdateCorporatePromo;

public class UpdateCorporatePromoHandler : IRequestHandler<UpdateCorporatePromoCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateCorporatePromoHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<bool>> Handle(
        UpdateCorporatePromoCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CorporatePromos
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("PROMO_NOT_FOUND", "Corporate promo not found.");

        var req = request.Request;
        entity.Title = req.Title;
        entity.Description = req.Description;
        entity.StartDate = req.StartDate;
        entity.EndDate = req.EndDate;
        entity.BannerUrl = req.BannerUrl;
        entity.IsActive = req.IsActive;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
