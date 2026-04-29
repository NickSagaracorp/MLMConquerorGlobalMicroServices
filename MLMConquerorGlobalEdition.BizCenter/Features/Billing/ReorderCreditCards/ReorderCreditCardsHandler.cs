using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.ReorderCreditCards;

public class ReorderCreditCardsHandler : IRequestHandler<ReorderCreditCardsCommand, Result<bool>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public ReorderCreditCardsHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<bool>> Handle(ReorderCreditCardsCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var ordered  = command.Request.OrderedCardIds ?? new();

        if (ordered.Count == 0)
            return Result<bool>.Failure("EMPTY_ORDER", "No card order was provided.");

        var cards = await _db.CreditCards
            .Where(c => c.MemberId == memberId && !c.IsDeleted)
            .ToListAsync(ct);

        if (cards.Count != ordered.Count)
            return Result<bool>.Failure("ORDER_MISMATCH",
                "The submitted order does not include all of your active cards.");

        var ownedIds = cards.Select(c => c.Id).ToHashSet();
        if (!ordered.All(id => ownedIds.Contains(id)))
            return Result<bool>.Failure("UNKNOWN_CARD",
                "One or more cards in the submitted order do not belong to you.");

        var now = _dateTime.UtcNow;
        for (var i = 0; i < ordered.Count; i++)
        {
            var card = cards.First(c => c.Id == ordered[i]);
            card.Priority       = i;
            card.LastUpdateDate = now;
            card.LastUpdateBy   = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
