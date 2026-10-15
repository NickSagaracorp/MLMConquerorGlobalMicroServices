using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using ICacheService          = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;

/// <summary>
/// Transfers a SINGLE token instance (identified by its TokenCode) from the current owner
/// to a recipient who must be in the current owner's enrollment subtree.
///
/// Same rules as <see cref="DistributeToken.DistributeTokenHandler"/> (which moves N codes of a type
/// at once, oldest first): the same code travels to the recipient, preserving end-to-end
/// chain-of-custody for fraud audits.
///
/// Effects on success:
///   • TokenTransaction(ReferenceId=code): MemberId ← recipient, PreviousOwnerMemberId ← sender, Status = Distributed
///   • New ledger TokenTransaction (no ReferenceId): TransactionType=Distributed, MemberId=sender, DistributedToMemberId=recipient
///   • TokenBalance: -1 sender, +1 recipient (caches updated to keep BizCenter widgets accurate)
///   • Push notification to recipient
///
/// The instance is read and moved under the funds lock of both members
/// (<see cref="IMemberFundsGate"/>, see DistributeTokenHandler): a transfer and a distribution of
/// the same owner cannot both move the same code. Busy → <see cref="TokenPurchaseErrors.Busy"/>.
/// </summary>
public class TransferTokenInstanceHandler : IRequestHandler<TransferTokenInstanceCommand, Result<TransferTokenInstanceResponse>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService _cache;
    private readonly IPushNotificationService _push;
    private readonly ILogger<TransferTokenInstanceHandler> _logger;
    private readonly IMemberFundsGate _gate;

    public TransferTokenInstanceHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        ICacheService cache,
        IPushNotificationService push,
        ILogger<TransferTokenInstanceHandler> logger,
        IMemberFundsGate gate)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _cache       = cache;
        _push        = push;
        _logger      = logger;
        _gate        = gate;
    }

    public async Task<Result<TransferTokenInstanceResponse>> Handle(
        TransferTokenInstanceCommand command, CancellationToken ct)
    {
        var senderId  = _currentUser.MemberId;
        var requested = command.RecipientMemberId.Trim();
        var code      = command.TokenCode.Trim().ToUpperInvariant();
        var now       = _dateTime.Now;

        if (string.Equals(senderId, requested, StringComparison.OrdinalIgnoreCase))
            return Result<TransferTokenInstanceResponse>.Failure(
                "INVALID_RECIPIENT", "Cannot transfer a token to yourself.");

        // Recipient must exist; keep the id as stored (it names the funds lock).
        var recipient = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == requested)
            .Select(m => m.MemberId)
            .FirstOrDefaultAsync(ct);
        if (recipient is null)
            return Result<TransferTokenInstanceResponse>.Failure(
                "RECIPIENT_NOT_FOUND", "Recipient member not found.");

        // Recipient must be in the sender's enrollment subtree.
        var inTeam = await TokenCustody.IsInEnrollmentTeamAsync(_db, senderId, recipient, ct);
        if (inTeam is null)
            return Result<TransferTokenInstanceResponse>.Failure(
                "SENDER_NOT_IN_TREE", "Sender's enrollment node not found.");
        if (inTeam == false)
        {
            _logger.LogInformation(
                "TransferToken: recipient '{Recipient}' is not in sender '{Sender}' subtree",
                recipient, senderId);
            return Result<TransferTokenInstanceResponse>.Failure(
                "RECIPIENT_NOT_IN_DOWNLINE", "Recipient must be a member of your enrollment team.");
        }

        var tx = await TokenCustody.EnterBothAsync(_gate, senderId, recipient, ct);
        if (tx is null)
            return Result<TransferTokenInstanceResponse>.Failure(TokenPurchaseErrors.Busy,
                "Another token operation is in progress. Please try again in a moment.");

        await using (tx)
        {
            // Read under the lock: whatever another operation committed while we waited is visible here.
            var instance = await _db.TokenTransactions
                .FirstOrDefaultAsync(t => t.ReferenceId == code, ct);
            var failure = Check(instance, senderId, code, now);
            if (failure is not null)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return failure;
            }

            instance!.PreviousOwnerMemberId = instance.MemberId;
            instance.MemberId               = recipient;
            instance.Status                 = TokenInstanceStatus.Distributed;

            // Ledger row capturing the transfer event (no ReferenceId so it doesn't appear as a redeemable instance).
            _db.TokenTransactions.Add(new TokenTransaction
            {
                MemberId              = senderId,
                TokenTypeId           = instance.TokenTypeId,
                TransactionType       = TokenTransactionType.Distributed,
                Quantity              = 1,
                DistributedToMemberId = recipient,
                ReferenceId           = null,
                OriginalOwnerMemberId = instance.OriginalOwnerMemberId,
                PreviousOwnerMemberId = senderId,
                Status                = TokenInstanceStatus.Distributed,
                CreatedBy             = senderId,
                CreationDate          = now,
                Notes                 = string.IsNullOrWhiteSpace(command.Notes)
                                            ? $"Token {code} transferred to {recipient}"
                                            : command.Notes
            });

            await TokenCustody.MoveBalanceAsync(_db, senderId, recipient, instance.TokenTypeId, 1,
                now, _currentUser.UserId, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // Best-effort cache invalidation + push notification.
        await Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(senderId), ct),
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(recipient), ct));

        _ = _push.SendAsync(
            recipient,
            NotificationEvents.TokenReceived,
            "Token Received",
            "You have received a token from your sponsor.",
            ct);

        return Result<TransferTokenInstanceResponse>.Success(
            new TransferTokenInstanceResponse(code, recipient, now));
    }

    private Result<TransferTokenInstanceResponse>? Check(
        TokenTransaction? instance, string senderId, string code, DateTime now)
    {
        if (instance is null)
            return Result<TransferTokenInstanceResponse>.Failure("TOKEN_NOT_FOUND", "Token code not found.");

        // Sender must be the current owner.
        if (!string.Equals(instance.MemberId, senderId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "TransferToken: sender '{Sender}' is not the current owner of '{Code}' (owner={Owner})",
                senderId, code, instance.MemberId);
            return Result<TransferTokenInstanceResponse>.Failure(
                "TOKEN_NOT_OWNED", "You are not the current owner of this token.");
        }

        // Status and expiry must allow transfer (same rule as distribution).
        if (instance.Status is not (TokenInstanceStatus.Issued or TokenInstanceStatus.Distributed)
            || (instance.ExpiresAt is not null && instance.ExpiresAt <= now))
            return Result<TransferTokenInstanceResponse>.Failure(
                "TOKEN_NOT_TRANSFERABLE", "This token can no longer be transferred.");

        return null;
    }
}
