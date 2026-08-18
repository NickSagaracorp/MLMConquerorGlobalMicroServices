using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ValidateMemberPayoutAccount;

public class ValidateMemberPayoutAccountHandler
    : IRequestHandler<ValidateMemberPayoutAccountCommand, Result<PayoutAccountValidationDto>>
{
    private readonly AppDbContext _db;
    private readonly IPayoutGatewayResolver _resolver;

    public ValidateMemberPayoutAccountHandler(AppDbContext db, IPayoutGatewayResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    public async Task<Result<PayoutAccountValidationDto>> Handle(
        ValidateMemberPayoutAccountCommand request, CancellationToken ct)
    {
        var wallet = await _db.Wallets
            .AsNoTracking()
            .Where(w => w.MemberId == request.MemberId && !w.IsDeleted && w.Status == WalletStatus.Approved)
            .OrderByDescending(w => w.IsPreferred)
            .FirstOrDefaultAsync(ct);

        if (wallet is null || string.IsNullOrWhiteSpace(wallet.AccountIdentifier))
            return Result<PayoutAccountValidationDto>.Failure(
                "NO_PAYOUT_WALLET", "Member has no approved payout wallet with an account");

        var gatewayResult = _resolver.Resolve(wallet.WalletType);
        if (!gatewayResult.IsSuccess)
            return Result<PayoutAccountValidationDto>.Failure(gatewayResult.ErrorCode!, gatewayResult.Error!);

        var validate = await gatewayResult.Value!.ValidateAccountAsync(new PayoutAccountContext
        {
            MemberId = request.MemberId,
            WalletType = wallet.WalletType,
            AccountIdentifier = wallet.AccountIdentifier!
        }, ct);

        if (!validate.IsSuccess)
            return Result<PayoutAccountValidationDto>.Failure(validate.ErrorCode!, validate.Error!);

        return Result<PayoutAccountValidationDto>.Success(new PayoutAccountValidationDto
        {
            MemberId = request.MemberId,
            Exists = validate.Value!.Exists,
            GatewayMessage = validate.Value.GatewayMessage
        });
    }
}
