using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.UpdateTokenBalance;

public class UpdateTokenBalanceHandler : IRequestHandler<UpdateTokenBalanceCommand, Result<AdminTokenBalanceDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateTokenBalanceHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AdminTokenBalanceDto>> Handle(
        UpdateTokenBalanceCommand request, CancellationToken cancellationToken)
    {
        var balance = await _db.TokenBalances
            .FirstOrDefaultAsync(b => b.Id == request.TokenBalanceId, cancellationToken);

        if (balance is null)
            return Result<AdminTokenBalanceDto>.Failure("TOKEN_BALANCE_NOT_FOUND", $"Token balance '{request.TokenBalanceId}' not found.");

        balance.Balance = request.Request.Balance;
        balance.LastUpdateDate = _dateTime.Now;
        balance.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

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
