using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public class TokenRedemptionService : ITokenRedemptionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TokenRedemptionService> _logger;

    private const string GenericMessage = "This token is not valid for this signup.";

    public TokenRedemptionService(AppDbContext db, ILogger<TokenRedemptionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<bool>> RedeemForSignupAsync(
        string tokenCode,
        string newMemberId,
        string orderId,
        IReadOnlyCollection<string> selectedProductIds,
        DateTime now,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenCode))
            return Result<bool>.Failure("TOKEN_NOT_VALID", GenericMessage);

        var code = tokenCode.Trim().ToUpperInvariant();

        // Tracked load so we can mutate Status / UsedAt / etc.
        var instance = await _db.TokenTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == code, ct);

        if (instance is null)
        {
            _logger.LogInformation("RedeemToken: code '{Code}' not found", code);
            return Result<bool>.Failure("TOKEN_NOT_VALID", GenericMessage);
        }

        var tokenType = await _db.TokenTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == instance.TokenTypeId, ct);

        if (tokenType is null)
        {
            _logger.LogWarning("RedeemToken: code '{Code}' references missing TokenTypeId={TypeId}",
                code, instance.TokenTypeId);
            return Result<bool>.Failure("TOKEN_NOT_VALID", GenericMessage);
        }

        var grantedLinks = await _db.TokenTypeProducts
            .AsNoTracking()
            .Where(p => p.TokenTypeId == tokenType.Id)
            .ToListAsync(ct);

        try
        {
            TokenInstanceSignupValidator.Validate(
                instance, tokenType, grantedLinks,
                selectedProductIds, now);
        }
        catch (TokenProductMismatchException)
        {
            // Resolve product names for a friendlier message — IDs alone don't help the user.
            // Materialize the allowed-product-ID set in memory first so EF can translate the
            // resulting query (nested filtering of an enum-bearing collection isn't translatable).
            var allowedIds = grantedLinks
                .Where(g => g.Role == TokenProductRole.Granted)
                .Select(g => g.ProductId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var allowedNames = await _db.Products
                .AsNoTracking()
                .Where(p => allowedIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .Select(p => p.Name)
                .ToListAsync(ct);

            var nameList = allowedNames.Count > 0 ? string.Join(", ", allowedNames) : "(no products configured)";
            return Result<bool>.Failure("TOKEN_PRODUCT_MISMATCH",
                $"This token only covers: {nameList}. Please adjust your product selection.");
        }
        catch (DomainException ex)
        {
            _logger.LogInformation("RedeemToken: code '{Code}' rejected ({DomainCode})", code, ex.Code);
            return Result<bool>.Failure("TOKEN_NOT_VALID", GenericMessage);
        }

        // Atomic consume + ledger. Save not invoked here — the caller wraps the entire signup
        // completion in a single SaveChangesAsync so token + member + order are persisted together.
        var lastOwner = instance.MemberId;

        instance.Status         = TokenInstanceStatus.Used;
        instance.UsedByMemberId = newMemberId;
        instance.UsedAt         = now;
        instance.UsedOnOrderId  = orderId;

        // Ledger row — pure event, no ReferenceId so it doesn't show up as a redeemable instance.
        await _db.TokenTransactions.AddAsync(new TokenTransaction
        {
            MemberId        = lastOwner,
            TokenTypeId     = instance.TokenTypeId,
            TransactionType = TokenTransactionType.Used,
            Quantity        = 1,
            UsedByMemberId  = newMemberId,
            UsedAt          = now,
            UsedOnOrderId   = orderId,
            Status          = TokenInstanceStatus.Used,
            ReferenceId     = null,
            CreatedBy       = newMemberId,
            CreationDate    = now,
            Notes           = $"Token {code} redeemed at signup"
        }, ct);

        // Decrement the previous owner's aggregate balance cache so their displayed counts stay correct.
        var balance = await _db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == lastOwner && tb.TokenTypeId == instance.TokenTypeId, ct);

        if (balance is not null && balance.Balance > 0)
        {
            balance.Balance       -= 1;
            balance.LastUpdateDate = now;
            balance.LastUpdateBy   = newMemberId;
        }

        return Result<bool>.Success(true);
    }
}
