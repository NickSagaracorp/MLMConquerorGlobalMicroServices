using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.DeleteCreditCard;

public class DeleteCreditCardHandler : IRequestHandler<DeleteCreditCardCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteCreditCardHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(DeleteCreditCardCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var card = await _db.CreditCards
            .FirstOrDefaultAsync(c => c.Id == command.CardId && c.MemberId == memberId && !c.IsDeleted, ct);

        if (card is null)
            return Result<bool>.Failure("CARD_NOT_FOUND", "Credit card not found.");

        card.IsDefault = false;
        card.IsDeleted = true;
        card.DeletedAt = _dateTime.UtcNow;
        card.DeletedBy = _currentUser.UserId;
        card.LastUpdateDate = _dateTime.UtcNow;
        card.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
