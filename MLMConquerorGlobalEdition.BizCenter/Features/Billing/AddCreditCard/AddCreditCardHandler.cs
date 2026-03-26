using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.AddCreditCard;

public class AddCreditCardHandler : IRequestHandler<AddCreditCardCommand, Result<CreditCardDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AddCreditCardHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<CreditCardDto>> Handle(AddCreditCardCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req = command.Request;

        var now = _dateTime.UtcNow;
        var isExpired = req.ExpiryYear < now.Year ||
                        (req.ExpiryYear == now.Year && req.ExpiryMonth < now.Month);

        var card = new MemberCreditCard
        {
            Id = Guid.NewGuid().ToString(),
            MemberId = memberId,
            Last4 = req.Last4,
            First6 = req.First6,
            CardBrand = req.CardBrand,
            ExpiryMonth = req.ExpiryMonth,
            ExpiryYear = req.ExpiryYear,
            Gateway = req.Gateway,
            GatewayToken = req.GatewayToken,
            CardToken = req.CardToken,
            MaskedCardNumber = req.MaskedCardNumber,
            IsDefault = false,
            IsExpired = isExpired,
            CreatedBy = _currentUser.UserId,
            CreationDate = now
        };

        _db.CreditCards.Add(card);
        await _db.SaveChangesAsync(ct);

        var dto = new CreditCardDto
        {
            Id = card.Id,
            Last4 = card.Last4,
            First6 = card.First6,
            CardBrand = card.CardBrand,
            ExpiryMonth = card.ExpiryMonth,
            ExpiryYear = card.ExpiryYear,
            IsDefault = card.IsDefault,
            IsExpired = card.IsExpired,
            MaskedCardNumber = card.MaskedCardNumber
        };

        return Result<CreditCardDto>.Success(dto);
    }
}
