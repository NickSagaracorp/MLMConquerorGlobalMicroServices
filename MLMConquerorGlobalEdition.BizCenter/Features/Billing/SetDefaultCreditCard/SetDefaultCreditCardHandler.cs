using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.SetDefaultCreditCard;

public class SetDefaultCreditCardHandler : IRequestHandler<SetDefaultCreditCardCommand, Result<bool>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public SetDefaultCreditCardHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<bool>> Handle(SetDefaultCreditCardCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var target = await _db.CreditCards
            .FirstOrDefaultAsync(c => c.Id == command.CardId
                                   && c.MemberId == memberId
                                   && !c.IsDeleted, ct);

        if (target is null)
            return Result<bool>.Failure("CARD_NOT_FOUND", "Credit card not found.");

        if (target.IsExpired)
            return Result<bool>.Failure("CARD_EXPIRED",
                "This card is expired and cannot be set as default.");

        var siblings = await _db.CreditCards
            .Where(c => c.MemberId == memberId && !c.IsDeleted && c.IsDefault)
            .ToListAsync(ct);

        var now = _dateTime.UtcNow;
        foreach (var s in siblings)
        {
            s.IsDefault      = false;
            s.LastUpdateDate = now;
            s.LastUpdateBy   = _currentUser.UserId;
        }

        target.IsDefault      = true;
        target.LastUpdateDate = now;
        target.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
