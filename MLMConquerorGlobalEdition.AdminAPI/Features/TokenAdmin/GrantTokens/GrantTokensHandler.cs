using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GrantTokens;

public class GrantTokensHandler : IRequestHandler<GrantTokensCommand, Result<AdminTokenBalanceDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public GrantTokensHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AdminTokenBalanceDto>> Handle(
        GrantTokensCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;

        var tokenType = await _db.TokenTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == req.TokenTypeId, cancellationToken);

        if (tokenType is null)
            return Result<AdminTokenBalanceDto>.Failure("TOKEN_TYPE_NOT_FOUND", $"Token type '{req.TokenTypeId}' not found.");

        var now = _dateTime.Now;

        var balance = await _db.TokenBalances
            .FirstOrDefaultAsync(b => b.MemberId == req.MemberId && b.TokenTypeId == req.TokenTypeId, cancellationToken);

        if (balance is null)
        {
            balance = new TokenBalance
            {
                MemberId = req.MemberId,
                TokenTypeId = req.TokenTypeId,
                Balance = 0,
                CreationDate = now,
                CreatedBy = _currentUser.UserId,
                LastUpdateDate = now,
                LastUpdateBy = _currentUser.UserId
            };
            await _db.TokenBalances.AddAsync(balance, cancellationToken);
        }

        balance.Add(req.Quantity);
        balance.LastUpdateDate = now;
        balance.LastUpdateBy = _currentUser.UserId;

        // Create one individual TokenTransaction per token, each with its own unique TokenCode
        var generatedCodes = new List<string>();
        for (var i = 0; i < req.Quantity; i++)
        {
            var tokenCode = TokenCodeGenerator.Generate();
            generatedCodes.Add(tokenCode);

            var transaction = new TokenTransaction
            {
                MemberId = req.MemberId,
                TokenTypeId = req.TokenTypeId,
                TransactionType = TokenTransactionType.AdminGranted,
                Quantity = 1,
                ReferenceId = tokenCode,
                Notes = req.Notes,
                CreationDate = now,
                CreatedBy = _currentUser.UserId
            };
            await _db.TokenTransactions.AddAsync(transaction, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = new AdminTokenBalanceDto
        {
            TokenBalanceId = balance.Id,
            MemberId = balance.MemberId,
            TokenTypeName = tokenType.Name,
            IsGuestPass = tokenType.IsGuestPass,
            Balance = balance.Balance,
            GeneratedTokenCodes = generatedCodes
        };

        return Result<AdminTokenBalanceDto>.Success(dto);
    }
}
