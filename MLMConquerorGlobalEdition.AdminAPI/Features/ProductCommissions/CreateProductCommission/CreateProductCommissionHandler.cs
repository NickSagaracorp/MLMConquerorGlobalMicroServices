using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.CreateProductCommission;

public class CreateProductCommissionHandler
    : IRequestHandler<CreateProductCommissionCommand, Result<int>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateProductCommissionHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<int>> Handle(
        CreateProductCommissionCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new ProductCommission
        {
            ProductId = req.ProductId,
            TriggerSponsorBonus = req.TriggerSponsorBonus,
            TriggerBuilderBonus = req.TriggerBuilderBonus,
            TriggerSponsorBonusTurbo = req.TriggerSponsorBonusTurbo,
            TriggerBuilderBonusTurbo = req.TriggerBuilderBonusTurbo,
            TriggerFastStartBonus = req.TriggerFastStartBonus,
            TriggerBoostBonus = req.TriggerBoostBonus,
            CarBonusEligible = req.CarBonusEligible,
            PresidentialBonusEligible = req.PresidentialBonusEligible,
            EligibleMembershipResidual = req.EligibleMembershipResidual,
            EligibleDailyResidual = req.EligibleDailyResidual,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.ProductCommissions.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
