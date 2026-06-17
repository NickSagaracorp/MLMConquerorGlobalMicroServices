using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatchDetail;

public class GetPayoutBatchDetailHandler
    : IRequestHandler<GetPayoutBatchDetailQuery, Result<PayoutBatchDetailDto>>
{
    private readonly AppDbContext _db;

    public GetPayoutBatchDetailHandler(AppDbContext db) => _db = db;

    public async Task<Result<PayoutBatchDetailDto>> Handle(
        GetPayoutBatchDetailQuery request, CancellationToken ct)
    {
        var batch = await _db.PayoutBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, ct);

        if (batch is null)
            return Result<PayoutBatchDetailDto>.Failure(
                "PAYOUT_BATCH_NOT_FOUND", $"Batch '{request.BatchId}' not found");

        var members = await _db.PayoutAttempts.AsNoTracking()
            .Where(a => a.PayoutBatchId == request.BatchId)
            .OrderBy(a => a.Id)
            .Select(a => new PayoutBatchMemberDto
            {
                PayoutAttemptId = a.Id,
                MemberId = a.MemberId,
                AmountUsd = a.AmountUsd,
                Outcome = a.Outcome,
                GatewayErrorCode = a.GatewayErrorCode,
                GatewayErrorMessage = a.GatewayErrorMessage,
                GatewayTransactionId = a.GatewayTransactionId
            })
            .ToListAsync(ct);

        var dto = new PayoutBatchDetailDto
        {
            Id = batch.Id,
            WalletType = batch.WalletType,
            ProcessDateUtc = batch.ProcessDateUtc,
            Status = batch.Status,
            MemberCount = batch.MemberCount,
            TotalAmountUsd = batch.TotalAmountUsd,
            ExportCsvUrl = batch.ExportCsvUrl,
            ResultCsvUrl = batch.ResultCsvUrl,
            ReconciledBy = batch.ReconciledBy,
            ReconciledAt = batch.ReconciledAt,
            Notes = batch.Notes,
            CreationDate = batch.CreationDate,
            Members = members
        };

        return Result<PayoutBatchDetailDto>.Success(dto);
    }
}
