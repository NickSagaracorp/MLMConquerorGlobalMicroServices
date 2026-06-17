using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ResendPayoutReceipt;

public class ResendPayoutReceiptHandler : IRequestHandler<ResendPayoutReceiptCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IPayoutReceiptService _receipts;

    public ResendPayoutReceiptHandler(AppDbContext db, IPayoutReceiptService receipts)
    {
        _db = db;
        _receipts = receipts;
    }

    public async Task<Result<bool>> Handle(ResendPayoutReceiptCommand c, CancellationToken ct)
    {
        var attempt = await _db.PayoutAttempts.FirstOrDefaultAsync(x => x.Id == c.AttemptId, ct);
        if (attempt is null)
            return Result<bool>.Failure("PAYOUT_ATTEMPT_NOT_FOUND", "Payout attempt not found");

        if (attempt.Outcome != PayoutOutcome.Success)
            return Result<bool>.Failure("PAYOUT_NOT_SUCCESSFUL", "Only successful payouts have a receipt");

        var sent = await _receipts.ResendReceiptAsync(attempt, ct);
        return Result<bool>.Success(sent);
    }
}
