using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Wallet.GetWallet;

public class GetWalletHandler : IRequestHandler<GetWalletQuery, Result<WalletDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetWalletHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<WalletDto>> Handle(GetWalletQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.MemberId == memberId && w.IsPreferred, ct);

        if (wallet is null)
            return Result<WalletDto>.Failure("WALLET_NOT_FOUND", "No preferred wallet found for this member.");

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
