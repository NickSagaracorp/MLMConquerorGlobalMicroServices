using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IEncryptionService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEncryptionService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetCreditCards;

public class GetCreditCardsHandler : IRequestHandler<GetCreditCardsQuery, Result<IEnumerable<CreditCardDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEncryptionService  _encryption;

    public GetCreditCardsHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IEncryptionService  encryption)
    {
        _db          = db;
        _currentUser = currentUser;
        _encryption  = encryption;
    }

    public async Task<Result<IEnumerable<CreditCardDto>>> Handle(GetCreditCardsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Pull encrypted columns and decrypt expiry in memory so the UI can show MM/YY.
        // CVV is never decrypted — the Add handler is the only place it ever touches plaintext.
        var rows = await _db.CreditCards
            .AsNoTracking()
            .Where(c => c.MemberId == memberId && !c.IsDeleted)
            .OrderBy(c => c.Priority)
            .ThenByDescending(c => c.CreationDate)
            .Select(c => new
            {
                c.Id, c.Last4, c.First6, c.CardBrand,
                c.EncryptedExpiry, c.IsDefault, c.IsExpired,
                c.Priority, c.MaskedCardNumber
            })
            .ToListAsync(ct);

        var cards = rows.Select(r =>
        {
            var (m, y) = DecryptExpiry(r.EncryptedExpiry);
            return new CreditCardDto
            {
                Id               = r.Id,
                Last4            = r.Last4,
                First6           = r.First6,
                CardBrand        = r.CardBrand,
                ExpiryMonth      = m,
                ExpiryYear       = y,
                IsDefault        = r.IsDefault,
                IsExpired        = r.IsExpired,
                Priority         = r.Priority,
                MaskedCardNumber = r.MaskedCardNumber
            };
        }).ToList();

        return Result<IEnumerable<CreditCardDto>>.Success(cards);
    }

    private (int Month, int Year) DecryptExpiry(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return (0, 0);
        try
        {
            var plain = _encryption.Decrypt(encrypted);     // "MM/YYYY"
            var parts = plain.Split('/');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var m)
                && int.TryParse(parts[1], out var y))
                return (m, y);
        }
        catch { /* corrupted ciphertext — fall through to "unknown" */ }
        return (0, 0);
    }
}
