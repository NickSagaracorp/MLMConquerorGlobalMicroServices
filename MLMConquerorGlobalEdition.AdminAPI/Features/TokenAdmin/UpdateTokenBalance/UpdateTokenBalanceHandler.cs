using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.UpdateTokenBalance;

/// <summary>
/// Ajusta a mano el <see cref="TokenBalance"/> de un miembro. Va bajo su candado de fondos
/// (<see cref="IMemberFundsGate"/>), como comprar, distribuir y transferir, y relee el saldo dentro:
/// así el ajuste no se pisa con una suma o resta simultánea. Ocupado → <see cref="TokenPurchaseErrors.Busy"/>.
/// </summary>
public class UpdateTokenBalanceHandler : IRequestHandler<UpdateTokenBalanceCommand, Result<AdminTokenBalanceDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IMemberFundsGate _gate;

    public UpdateTokenBalanceHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IMemberFundsGate gate)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _gate = gate;
    }

    public async Task<Result<AdminTokenBalanceDto>> Handle(
        UpdateTokenBalanceCommand request, CancellationToken cancellationToken)
    {
        var memberId = await _db.TokenBalances.AsNoTracking()
            .Where(b => b.Id == request.TokenBalanceId)
            .Select(b => b.MemberId)
            .FirstOrDefaultAsync(cancellationToken);

        if (memberId is null)
            return Result<AdminTokenBalanceDto>.Failure("TOKEN_BALANCE_NOT_FOUND", $"Token balance '{request.TokenBalanceId}' not found.");

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx;
        try
        {
            tx = await _gate.EnterAsync(memberId, cancellationToken);
        }
        catch (MemberFundsBusyException)
        {
            return Result<AdminTokenBalanceDto>.Failure(TokenPurchaseErrors.Busy,
                "Another token operation for this member is in progress. Try again in a moment.");
        }
        await using var _ = tx;

        var balance = await _db.TokenBalances
            .FirstAsync(b => b.Id == request.TokenBalanceId, cancellationToken);

        balance.Balance = request.Request.Balance;
        balance.LastUpdateDate = _dateTime.Now;
        balance.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var tokenType = await _db.TokenTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(tt => tt.Id == balance.TokenTypeId, cancellationToken);

        var dto = new AdminTokenBalanceDto
        {
            TokenBalanceId = balance.Id,
            MemberId = balance.MemberId,
            TokenTypeName = tokenType?.Name ?? string.Empty,
            IsGuestPass = tokenType?.IsGuestPass ?? false,
            Balance = balance.Balance
        };

        return Result<AdminTokenBalanceDto>.Success(dto);
    }
}
