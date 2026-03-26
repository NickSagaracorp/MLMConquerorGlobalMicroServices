using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.CreateTokenTypeCommission;

public class CreateTokenTypeCommissionHandler
    : IRequestHandler<CreateTokenTypeCommissionCommand, Result<int>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateTokenTypeCommissionHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<int>> Handle(
        CreateTokenTypeCommissionCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new TokenTypeCommission
        {
            TokenTypeId = req.TokenTypeId,
            CommissionTypeId = req.CommissionTypeId,
            CommissionPerToken = req.CommissionPerToken,
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

        await _db.TokenTypeCommissions.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
