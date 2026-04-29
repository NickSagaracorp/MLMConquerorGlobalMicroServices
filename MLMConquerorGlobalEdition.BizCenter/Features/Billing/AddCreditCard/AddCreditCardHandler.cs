using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.BizCenter.Services.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IEncryptionService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEncryptionService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.AddCreditCard;

public class AddCreditCardHandler : IRequestHandler<AddCreditCardCommand, Result<CreditCardDto>>
{
    private readonly AppDbContext              _db;
    private readonly ICurrentUserService       _currentUser;
    private readonly IDateTimeProvider         _dateTime;
    private readonly ICardTokenizationService  _tokenizer;
    private readonly IEncryptionService        _encryption;

    public AddCreditCardHandler(
        AppDbContext              db,
        ICurrentUserService       currentUser,
        IDateTimeProvider         dateTime,
        ICardTokenizationService  tokenizer,
        IEncryptionService        encryption)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _tokenizer   = tokenizer;
        _encryption  = encryption;
    }

    public async Task<Result<CreditCardDto>> Handle(AddCreditCardCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req      = command.Request;

        var digits = new string(req.CardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length < 13 || digits.Length > 19)
            return Result<CreditCardDto>.Failure("INVALID_CARD_NUMBER",
                "Card number must be 13–19 digits.");

        if (req.ExpiryMonth is < 1 or > 12)
            return Result<CreditCardDto>.Failure("INVALID_EXPIRY_MONTH",
                "Expiry month must be between 1 and 12.");

        var now       = _dateTime.UtcNow;
        var isExpired = req.ExpiryYear < now.Year ||
                        (req.ExpiryYear == now.Year && req.ExpiryMonth < now.Month);

        // Reject already-expired cards up front — saving them only creates noise
        // in the wallet and can never produce a successful charge.
        if (isExpired)
            return Result<CreditCardDto>.Failure("CARD_ALREADY_EXPIRED",
                "This card is already expired. Please use a card with a future expiration date.");

        // 1) Tokenize with the gateway (real call in prod, simulated locally).
        var tokenization = await _tokenizer.TokenizeAsync(
            digits, req.ExpiryMonth, req.ExpiryYear,
            req.CardholderName ?? string.Empty,
            req.Cvv ?? string.Empty, ct);

        // 2) Derive display + persisted values from the raw PAN, then drop it.
        //    First 12 digits are encrypted and kept only for fraud-team lookups;
        //    only the last 4 ever leave the server in plain text.
        var brand        = _tokenizer.DetectBrand(digits);
        var first6       = digits.Length >= 6 ? digits[..6] : digits;
        var last4        = digits.Length >= 4 ? digits[^4..] : digits;
        var encryptedPan = _encryption.Encrypt(digits[..^4]); // everything except the last 4
        var masked       = new string('*', digits.Length - 4) + last4;

        // Expiry stored encrypted as "MM/YYYY" (decrypted on read).
        // CVV stored encrypted but NEVER decrypted for display — UI always shows ***.
        var encryptedExpiry = _encryption.Encrypt(
            $"{req.ExpiryMonth:00}/{req.ExpiryYear:0000}");
        var encryptedCvv = string.IsNullOrWhiteSpace(req.Cvv)
            ? null
            : _encryption.Encrypt(req.Cvv);

        // 3) Inspect the existing wallet to decide priority + default placement.
        var existingCards = await _db.CreditCards
            .Where(c => c.MemberId == memberId && !c.IsDeleted)
            .ToListAsync(ct);

        var nextPriority = existingCards.Count == 0
            ? 0
            : existingCards.Max(c => c.Priority) + 1;

        // Business rule: if the wallet already contains an expired card, the
        // member's recurring charges are presumably failing — promote the new
        // card to default immediately so billing can resume.
        var anyExpired      = existingCards.Any(c => c.IsExpired);
        var shouldBeDefault = anyExpired;

        if (shouldBeDefault)
        {
            foreach (var prevDefault in existingCards.Where(c => c.IsDefault))
            {
                prevDefault.IsDefault      = false;
                prevDefault.LastUpdateDate = now;
                prevDefault.LastUpdateBy   = _currentUser.UserId;
            }
        }

        var card = new MemberCreditCard
        {
            Id               = Guid.NewGuid().ToString(),
            MemberId         = memberId,
            Last4            = last4,
            First6           = first6,
            EncryptedPan     = encryptedPan,
            MaskedCardNumber = masked,
            CardBrand        = brand,
            EncryptedExpiry  = encryptedExpiry,
            EncryptedCvv     = encryptedCvv,
            Gateway          = tokenization.Gateway,
            GatewayToken     = tokenization.GatewayToken,
            CardToken        = tokenization.CardToken,
            IsDefault        = shouldBeDefault,
            IsExpired        = false, // we just rejected anything that was expired
            Priority         = nextPriority,
            CreatedBy        = _currentUser.UserId,
            CreationDate     = now,
            LastUpdateDate   = now,
            LastUpdateBy     = _currentUser.UserId
        };

        _db.CreditCards.Add(card);
        await _db.SaveChangesAsync(ct);

        return Result<CreditCardDto>.Success(new CreditCardDto
        {
            Id               = card.Id,
            Last4            = card.Last4,
            First6           = card.First6,
            CardBrand        = card.CardBrand,
            ExpiryMonth      = req.ExpiryMonth,
            ExpiryYear       = req.ExpiryYear,
            IsDefault        = card.IsDefault,
            IsExpired        = false,
            Priority         = card.Priority,
            MaskedCardNumber = card.MaskedCardNumber
        });
    }
}
