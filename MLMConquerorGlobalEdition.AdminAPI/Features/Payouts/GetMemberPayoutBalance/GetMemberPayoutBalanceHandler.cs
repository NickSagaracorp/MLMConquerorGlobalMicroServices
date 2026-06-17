using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetMemberPayoutBalance;

public class GetMemberPayoutBalanceHandler
    : IRequestHandler<GetMemberPayoutBalanceQuery, Result<MemberPayoutBalanceDto>>
{
    private readonly AppDbContext _db;
    private readonly IPayoutGatewayResolver _resolver;

    public GetMemberPayoutBalanceHandler(AppDbContext db, IPayoutGatewayResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    public async Task<Result<MemberPayoutBalanceDto>> Handle(
        GetMemberPayoutBalanceQuery request, CancellationToken ct)
    {
        var wallet = await _db.Wallets
            .AsNoTracking()
            .Where(w => w.MemberId == request.MemberId && !w.IsDeleted && w.Status == WalletStatus.Approved)
            .OrderByDescending(w => w.IsPreferred)
            .FirstOrDefaultAsync(ct);

        if (wallet is null || string.IsNullOrWhiteSpace(wallet.AccountIdentifier))
            return Result<MemberPayoutBalanceDto>.Failure(
                "NO_PAYOUT_WALLET", "Member has no approved payout wallet with an account");

        var gatewayResult = _resolver.Resolve(wallet.WalletType);
        if (!gatewayResult.IsSuccess)
            return Result<MemberPayoutBalanceDto>.Failure(gatewayResult.ErrorCode!, gatewayResult.Error!);

        var balance = await gatewayResult.Value!.GetBalanceAsync(
            request.MemberId, wallet.AccountIdentifier!, ct);

        if (!balance.IsSuccess)
            return Result<MemberPayoutBalanceDto>.Failure(balance.ErrorCode!, balance.Error!);

        return Result<MemberPayoutBalanceDto>.Success(new MemberPayoutBalanceDto
        {
            MemberId = request.MemberId,
            Balance = balance.Value!.Balance,
            Currency = balance.Value.Currency
        });
    }
}
