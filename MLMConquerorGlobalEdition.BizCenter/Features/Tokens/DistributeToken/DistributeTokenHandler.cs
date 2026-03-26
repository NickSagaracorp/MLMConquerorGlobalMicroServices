using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;

public class DistributeTokenHandler : IRequestHandler<DistributeTokenCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DistributeTokenHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(DistributeTokenCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req = command.Request;
        var now = _dateTime.UtcNow;

        // Validate token type
        var tokenType = await _db.TokenTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(tt => tt.Id == req.TokenTypeId && tt.IsActive, ct);

        if (tokenType is null)
            return Result<bool>.Failure("TOKEN_TYPE_NOT_FOUND", "Token type not found or inactive.");

        // Validate recipient
        var recipientExists = await _db.MemberProfiles
            .AnyAsync(m => m.MemberId == req.RecipientMemberId, ct);

        if (!recipientExists)
            return Result<bool>.Failure("RECIPIENT_NOT_FOUND", "Recipient member not found.");

        if (memberId == req.RecipientMemberId)
            return Result<bool>.Failure("INVALID_RECIPIENT", "Cannot distribute tokens to yourself.");

        // Load sender balance — throws InsufficientTokenBalanceException if insufficient
        var senderBalance = await _db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == memberId && tb.TokenTypeId == req.TokenTypeId, ct);

        if (senderBalance is null || senderBalance.Balance < req.Quantity)
            throw new InsufficientTokenBalanceException();

        senderBalance.Deduct(req.Quantity);
        senderBalance.LastUpdateDate = now;
        senderBalance.LastUpdateBy = _currentUser.UserId;

        // Load or create receiver balance
        var receiverBalance = await _db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == req.RecipientMemberId && tb.TokenTypeId == req.TokenTypeId, ct);

        if (receiverBalance is null)
        {
            receiverBalance = new TokenBalance
            {
                Id = Guid.NewGuid().ToString(),
                MemberId = req.RecipientMemberId,
                TokenTypeId = req.TokenTypeId,
                Balance = 0,
                CreatedBy = _currentUser.UserId,
                CreationDate = now
            };
            _db.TokenBalances.Add(receiverBalance);
        }

        receiverBalance.Add(req.Quantity);
        receiverBalance.LastUpdateDate = now;
        receiverBalance.LastUpdateBy = _currentUser.UserId;

        // Single deduction record on sender side
        var senderTransaction = new TokenTransaction
        {
            MemberId = memberId,
            TokenTypeId = req.TokenTypeId,
            TransactionType = TokenTransactionType.Distributed,
            Quantity = req.Quantity,
            DistributedToMemberId = req.RecipientMemberId,
            CreatedBy = _currentUser.UserId,
            CreationDate = now
        };
        _db.TokenTransactions.Add(senderTransaction);

        // One individual TokenTransaction per token on receiver side, each with a unique TokenCode
        for (var i = 0; i < req.Quantity; i++)
        {
            var receiverTransaction = new TokenTransaction
            {
                MemberId = req.RecipientMemberId,
                TokenTypeId = req.TokenTypeId,
                TransactionType = TokenTransactionType.EarnedSignup,
                Quantity = 1,
                ReferenceId = TokenCodeGenerator.Generate(),
                CreatedBy = _currentUser.UserId,
                CreationDate = now
            };
            _db.TokenTransactions.Add(receiverTransaction);
        }

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
