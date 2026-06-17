using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAuditDetail;

public class GetPayoutAuditDetailHandler : IRequestHandler<GetPayoutAuditDetailQuery, Result<PayoutAuditDetailDto>>
{
    private readonly AppDbContext _db;

    public GetPayoutAuditDetailHandler(AppDbContext db) => _db = db;

    public async Task<Result<PayoutAuditDetailDto>> Handle(GetPayoutAuditDetailQuery r, CancellationToken ct)
    {
        var attempt = await _db.PayoutAttempts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == r.AttemptId, ct);

        if (attempt is null)
            return Result<PayoutAuditDetailDto>.Failure("PAYOUT_ATTEMPT_NOT_FOUND", "Payout attempt not found");

        var earnings = await _db.PayoutAttemptEarnings.AsNoTracking()
            .Where(e => e.PayoutAttemptId == attempt.Id)
            .Select(e => new PayoutAuditEarningDto
            {
                CommissionEarningId = e.CommissionEarningId,
                Amount = e.Amount
            })
            .ToListAsync(ct);

        var dto = new PayoutAuditDetailDto
        {
            PayoutAttemptId = attempt.Id,
            MemberId = attempt.MemberId,
            WalletTypeSnapshot = attempt.WalletTypeSnapshot,
            PayoutAccountSnapshot = attempt.PayoutAccountSnapshot,
            PayoutAccountMetaSnapshot = attempt.PayoutAccountMetaSnapshot,
            AmountUsd = attempt.AmountUsd,
            Outcome = attempt.Outcome,
            ProcessDateUtc = attempt.ProcessDateUtc,
            AttemptedAtUtc = attempt.AttemptedAtUtc,
            CompletedAtUtc = attempt.CompletedAtUtc,
            GatewayTransactionId = attempt.GatewayTransactionId,
            GatewayErrorCode = attempt.GatewayErrorCode,
            GatewayErrorMessage = attempt.GatewayErrorMessage,
            DisbursementMode = attempt.DisbursementMode.ToString(),
            ReceiptUrl = attempt.ReceiptUrl,
            ReceiptSha256 = attempt.ReceiptSha256,
            ReceiptLedgerSeq = attempt.ReceiptLedgerSeq,
            ReceiptAnchorRef = attempt.ReceiptAnchorRef,
            Earnings = earnings
        };

        return Result<PayoutAuditDetailDto>.Success(dto);
    }
}
