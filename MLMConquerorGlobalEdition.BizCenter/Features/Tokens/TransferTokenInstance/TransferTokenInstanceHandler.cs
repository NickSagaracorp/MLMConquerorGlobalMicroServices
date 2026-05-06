using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService          = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;

/// <summary>
/// Transfers a SINGLE token instance (identified by its TokenCode) from the current owner
/// to a recipient who must be in the current owner's enrollment subtree.
///
/// Differs from <see cref="DistributeToken.DistributeTokenHandler"/> (which distributes by
/// quantity and mints fresh codes for the recipient) — here the same code travels to the
/// recipient, preserving end-to-end chain-of-custody for fraud audits.
///
/// Effects on success:
///   • TokenTransaction(ReferenceId=code): MemberId ← recipient, PreviousOwnerMemberId ← sender, Status = Distributed
///   • New ledger TokenTransaction (no ReferenceId): TransactionType=Distributed, MemberId=sender, DistributedToMemberId=recipient
///   • TokenBalance: -1 sender, +1 recipient (caches updated to keep BizCenter widgets accurate)
///   • Push notification to recipient
/// </summary>
public class TransferTokenInstanceHandler : IRequestHandler<TransferTokenInstanceCommand, Result<TransferTokenInstanceResponse>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService _cache;
    private readonly IPushNotificationService _push;
    private readonly ILogger<TransferTokenInstanceHandler> _logger;

    public TransferTokenInstanceHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        ICacheService cache,
        IPushNotificationService push,
        ILogger<TransferTokenInstanceHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _cache       = cache;
        _push        = push;
        _logger      = logger;
    }

    public async Task<Result<TransferTokenInstanceResponse>> Handle(
        TransferTokenInstanceCommand command, CancellationToken ct)
    {
        var senderId  = _currentUser.MemberId;
        var recipient = command.RecipientMemberId.Trim();
        var code      = command.TokenCode.Trim().ToUpperInvariant();
        var now       = _dateTime.UtcNow;

        if (string.Equals(senderId, recipient, StringComparison.OrdinalIgnoreCase))
            return Result<TransferTokenInstanceResponse>.Failure(
                "INVALID_RECIPIENT", "Cannot transfer a token to yourself.");

        // Recipient must exist as an active member.
        var recipientExists = await _db.MemberProfiles
            .AnyAsync(m => m.MemberId == recipient, ct);
        if (!recipientExists)
            return Result<TransferTokenInstanceResponse>.Failure(
                "RECIPIENT_NOT_FOUND", "Recipient member not found.");

        // Recipient must be in the sender's enrollment subtree.
        // HierarchyPath of every descendant starts with the sender's path.
        var senderNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == senderId, ct);
        if (senderNode is null)
            return Result<TransferTokenInstanceResponse>.Failure(
                "SENDER_NOT_IN_TREE", "Sender's enrollment node not found.");

        var senderPath  = senderNode.HierarchyPath;
        var inDownline  = await _db.GenealogyTree
            .AsNoTracking()
            .AnyAsync(g => g.MemberId == recipient
                        && g.MemberId != senderId
                        && g.HierarchyPath.StartsWith(senderPath), ct);
        if (!inDownline)
        {
            _logger.LogInformation(
                "TransferToken: recipient '{Recipient}' is not in sender '{Sender}' subtree",
                recipient, senderId);
            return Result<TransferTokenInstanceResponse>.Failure(
                "RECIPIENT_NOT_IN_DOWNLINE", "Recipient must be a member of your enrollment team.");
        }

        // Locate the redeemable instance.
        var instance = await _db.TokenTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == code, ct);
        if (instance is null)
            return Result<TransferTokenInstanceResponse>.Failure(
                "TOKEN_NOT_FOUND", "Token code not found.");

        // Sender must be the current owner.
        if (!string.Equals(instance.MemberId, senderId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "TransferToken: sender '{Sender}' is not the current owner of '{Code}' (owner={Owner})",
                senderId, code, instance.MemberId);
            return Result<TransferTokenInstanceResponse>.Failure(
                "TOKEN_NOT_OWNED", "You are not the current owner of this token.");
        }

        // Status must allow transfer.
        if (instance.Status is not (TokenInstanceStatus.Issued or TokenInstanceStatus.Distributed))
            return Result<TransferTokenInstanceResponse>.Failure(
                "TOKEN_NOT_TRANSFERABLE", "This token can no longer be transferred.");

        // Mutate the instance: ownership shifts to recipient.
        instance.PreviousOwnerMemberId = instance.MemberId;
        instance.MemberId              = recipient;
        instance.Status                = TokenInstanceStatus.Distributed;

        // Ledger row capturing the transfer event (no ReferenceId so it doesn't appear as a redeemable instance).
        await _db.TokenTransactions.AddAsync(new TokenTransaction
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
        }, ct);

        // Aggregate balance caches: decrement sender, increment recipient.
        var senderBalance = await _db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == senderId && tb.TokenTypeId == instance.TokenTypeId, ct);
        if (senderBalance is not null && senderBalance.Balance > 0)
        {
            senderBalance.Balance       -= 1;
            senderBalance.LastUpdateDate = now;
            senderBalance.LastUpdateBy   = _currentUser.UserId;
        }

        var recipientBalance = await _db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == recipient && tb.TokenTypeId == instance.TokenTypeId, ct);
        if (recipientBalance is null)
        {
            recipientBalance = new TokenBalance
            {
                Id           = Guid.NewGuid().ToString(),
                MemberId     = recipient,
                TokenTypeId  = instance.TokenTypeId,
                Balance      = 0,
                CreatedBy    = _currentUser.UserId,
                CreationDate = now
            };
            _db.TokenBalances.Add(recipientBalance);
        }
        recipientBalance.Add(1);
        recipientBalance.LastUpdateDate = now;
        recipientBalance.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        // Best-effort cache invalidation + push notification.
        await Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(senderId), ct),
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(recipient), ct));

        _ = _push.SendAsync(
            recipient,
            NotificationEvents.TokenReceived,
            "Token Received",
            $"You have received a token from your sponsor.",
            ct);

        return Result<TransferTokenInstanceResponse>.Success(
            new TransferTokenInstanceResponse(code, recipient, now));
    }
}
