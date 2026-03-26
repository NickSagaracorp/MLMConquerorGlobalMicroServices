using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Wallet.UpdateWallet;

public class UpdateWalletHandler : IRequestHandler<UpdateWalletCommand, Result<WalletDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateWalletHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<WalletDto>> Handle(UpdateWalletCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req = command.Request;

        // Unset previous preferred wallet
        if (req.IsPreferred)
        {
            var existingPreferred = await _db.Wallets
                .Where(w => w.MemberId == memberId && w.IsPreferred)
                .ToListAsync(ct);

            foreach (var w in existingPreferred)
            {
                w.IsPreferred = false;
                w.LastUpdateDate = _dateTime.UtcNow;
                w.LastUpdateBy = _currentUser.UserId;
            }
        }

        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.MemberId == memberId && w.WalletType == req.WalletType, ct);

        if (wallet is null)
            return Result<WalletDto>.Failure("WALLET_NOT_FOUND", "No wallet found for the specified wallet type.");

        wallet.IsPreferred = req.IsPreferred;
        wallet.WalletType = req.WalletType;
        if (req.AccountIdentifier is not null)
            wallet.AccountIdentifier = req.AccountIdentifier;
        wallet.LastUpdateDate = _dateTime.UtcNow;
        wallet.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        var dto = new WalletDto
        {
            Id = wallet.Id,
            WalletType = wallet.WalletType.ToString(),
            Status = wallet.Status.ToString(),
            AccountIdentifier = wallet.AccountIdentifier,
            IsPreferred = wallet.IsPreferred
        };

        return Result<WalletDto>.Success(dto);
    }
}
