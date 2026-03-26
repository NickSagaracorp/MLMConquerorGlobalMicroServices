using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.UpdateProductCommission;

public class UpdateProductCommissionHandler
    : IRequestHandler<UpdateProductCommissionCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateProductCommissionHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<bool>> Handle(
        UpdateProductCommissionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductCommissions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("PRODUCT_COMMISSION_NOT_FOUND", "Product commission not found.");

        var req = request.Request;
        entity.TriggerSponsorBonus = req.TriggerSponsorBonus;
        entity.TriggerBuilderBonus = req.TriggerBuilderBonus;
        entity.TriggerSponsorBonusTurbo = req.TriggerSponsorBonusTurbo;
        entity.TriggerBuilderBonusTurbo = req.TriggerBuilderBonusTurbo;
        entity.TriggerFastStartBonus = req.TriggerFastStartBonus;
        entity.TriggerBoostBonus = req.TriggerBoostBonus;
        entity.CarBonusEligible = req.CarBonusEligible;
        entity.PresidentialBonusEligible = req.PresidentialBonusEligible;
        entity.EligibleMembershipResidual = req.EligibleMembershipResidual;
        entity.EligibleDailyResidual = req.EligibleDailyResidual;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
