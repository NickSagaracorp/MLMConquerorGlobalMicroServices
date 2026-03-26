using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetCreditCards;

public class GetCreditCardsHandler : IRequestHandler<GetCreditCardsQuery, Result<IEnumerable<CreditCardDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCreditCardsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<CreditCardDto>>> Handle(GetCreditCardsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var cards = await _db.CreditCards
            .AsNoTracking()
            .Where(c => c.MemberId == memberId && !c.IsDeleted)
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.CreationDate)
            .Select(c => new CreditCardDto
            {
                Id = c.Id,
                Last4 = c.Last4,
                First6 = c.First6,
                CardBrand = c.CardBrand,
                ExpiryMonth = c.ExpiryMonth,
                ExpiryYear = c.ExpiryYear,
                IsDefault = c.IsDefault,
                IsExpired = c.IsExpired,
                MaskedCardNumber = c.MaskedCardNumber
            })
            .ToListAsync(ct);

        return Result<IEnumerable<CreditCardDto>>.Success(cards);
    }
}
