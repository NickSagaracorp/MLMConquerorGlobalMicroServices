using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.VerifyPayoutReceipt;

public class VerifyPayoutReceiptHandler : IRequestHandler<VerifyPayoutReceiptCommand, Result<ReceiptVerificationDto>>
{
    private readonly AppDbContext _db;
    private readonly IReceiptVerificationService _verify;

    public VerifyPayoutReceiptHandler(AppDbContext db, IReceiptVerificationService verify)
    {
        _db = db;
        _verify = verify;
    }

    public async Task<Result<ReceiptVerificationDto>> Handle(VerifyPayoutReceiptCommand c, CancellationToken ct)
    {
        var attempt = await _db.PayoutAttempts.FirstOrDefaultAsync(x => x.Id == c.AttemptId, ct);
        if (attempt is null)
            return Result<ReceiptVerificationDto>.Failure("PAYOUT_ATTEMPT_NOT_FOUND", "Payout attempt not found");

        var v = await _verify.VerifyAsync(attempt, ct);

        return Result<ReceiptVerificationDto>.Success(new ReceiptVerificationDto
        {
            HasReceipt = v.HasReceipt,
            HashMatches = v.HashMatches,
            ChainValid = v.ChainValid,
            Anchored = v.Anchored,
            AnchorRef = v.AnchorRef,
            Detail = v.Detail
        });
    }
}
