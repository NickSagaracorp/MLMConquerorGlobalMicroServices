using System.Text;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;

public class PayoutBatchReconciliationService : IPayoutBatchReconciliationService
{
    private readonly AppDbContext _db;
    private readonly IPayoutCsvResolver _csv;
    private readonly IPayoutOrchestrator _orchestrator;
    private readonly IReceiptStorage _storage;
    private readonly Services.IDateTimeProvider _dateTime;
    private readonly Services.ICurrentUserService _currentUser;

    public PayoutBatchReconciliationService(
        AppDbContext db,
        IPayoutCsvResolver csv,
        IPayoutOrchestrator orchestrator,
        IReceiptStorage storage,
        Services.IDateTimeProvider dateTime,
        Services.ICurrentUserService currentUser)
    {
        _db = db;
        _csv = csv;
        _orchestrator = orchestrator;
        _storage = storage;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<BatchReconcileResult>> ReconcileFromResultsAsync(
        string batchId, string resultCsv, CancellationToken ct = default)
    {
        var batch = await _db.PayoutBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
            return Result<BatchReconcileResult>.Failure("PAYOUT_BATCH_NOT_FOUND", "Batch not found");
        if (batch.Status != PayoutBatchStatus.Exported)
            return Result<BatchReconcileResult>.Failure("PAYOUT_BATCH_NOT_OPEN", $"Batch is {batch.Status}");

        var parserResult = _csv.ResolveParser(batch.WalletType);
        if (!parserResult.IsSuccess)
            return Result<BatchReconcileResult>.Failure(parserResult.ErrorCode!, parserResult.Error!);

        var rows = parserResult.Value!.ParseResults(resultCsv);
        var attempts = await _db.PayoutAttempts.Where(a => a.PayoutBatchId == batchId).ToListAsync(ct);
        var byId = attempts.ToDictionary(a => a.Id);

        var now = _dateTime.Now;
        var actor = _currentUser.UserId;
        int succeeded = 0, failed = 0;

        // Persist the results CSV BEFORE the per-row loop so the uploaded file is preserved
        // even if the loop later throws on a specific row.
        batch.ResultCsvUrl = await _storage.SaveAsync(
            $"payout-batch-{batch.Id}-results.csv",
            Encoding.UTF8.GetBytes(resultCsv),
            ct);
        await _db.SaveChangesAsync(ct);

        foreach (var r in rows)
        {
            if (!byId.TryGetValue(r.PayoutAttemptId, out var attempt) || attempt.Outcome != PayoutOutcome.Pending)
                continue;

            if (r.Success)
            {
                try
                {
                    await _orchestrator.FinalizeSuccessAsync(attempt, r.GatewayTransactionId, null, ct);
                    succeeded++;
                }
                catch
                {
                    // A transient error on one row must not strand the rest of the batch.
                    // Count this row as failed so its earnings are freed and it can be retried.
                    attempt.Outcome = PayoutOutcome.Failed;
                    attempt.GatewayErrorCode = "FINALIZE_ERROR";
                    attempt.GatewayErrorMessage = "Finalization failed with an internal error; row may be retried";
                    attempt.CompletedAtUtc = now;
                    attempt.LastUpdateDate = now;
                    attempt.LastUpdateBy = actor;
                    failed++;
                    await _db.SaveChangesAsync(ct);
                }
            }
            else
            {
                // Setting Outcome = Failed frees the reserved earnings via the reservation guard
                // (guard excludes attempts with Outcome == Failed).
                attempt.Outcome = PayoutOutcome.Failed;
                attempt.GatewayErrorCode = r.ErrorCode;
                attempt.GatewayErrorMessage = r.ErrorMessage ?? "Gateway reported failure";
                attempt.CompletedAtUtc = now;
                attempt.LastUpdateDate = now;
                attempt.LastUpdateBy = actor;
                failed++;
            }
        }

        // Any attempt still Pending (no result row) is left for a future reconcile pass.
        var anyStillPending = attempts.Any(a => a.Outcome == PayoutOutcome.Pending);
        batch.Status = (failed > 0 || anyStillPending)
            ? PayoutBatchStatus.PartiallyReconciled
            : PayoutBatchStatus.Reconciled;

        batch.ReconciledBy = actor;
        batch.ReconciledAt = now;
        batch.LastUpdateDate = now;
        batch.LastUpdateBy = actor;

        await _db.SaveChangesAsync(ct);

        return Result<BatchReconcileResult>.Success(new BatchReconcileResult(succeeded, failed, batch.Status));
    }

    public async Task<Result<BatchReconcileResult>> MarkBatchPaidAsync(
        string batchId, CancellationToken ct = default)
    {
        var batch = await _db.PayoutBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
            return Result<BatchReconcileResult>.Failure("PAYOUT_BATCH_NOT_FOUND", "Batch not found");
        if (batch.Status != PayoutBatchStatus.Exported)
            return Result<BatchReconcileResult>.Failure("PAYOUT_BATCH_NOT_OPEN", $"Batch is {batch.Status}");

        var pending = await _db.PayoutAttempts
            .Where(a => a.PayoutBatchId == batchId && a.Outcome == PayoutOutcome.Pending)
            .ToListAsync(ct);

        foreach (var a in pending)
            await _orchestrator.FinalizeSuccessAsync(a, null, null, ct);

        var now = _dateTime.Now;
        var actor = _currentUser.UserId;
        batch.Status = PayoutBatchStatus.Reconciled;
        batch.ReconciledBy = actor;
        batch.ReconciledAt = now;
        batch.LastUpdateDate = now;
        batch.LastUpdateBy = actor;
        await _db.SaveChangesAsync(ct);

        return Result<BatchReconcileResult>.Success(new BatchReconcileResult(pending.Count, 0, batch.Status));
    }

    public async Task<Result<BatchReconcileResult>> CancelBatchAsync(
        string batchId, CancellationToken ct = default)
    {
        var batch = await _db.PayoutBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
            return Result<BatchReconcileResult>.Failure("PAYOUT_BATCH_NOT_FOUND", "Batch not found");
        if (batch.Status != PayoutBatchStatus.Exported)
            return Result<BatchReconcileResult>.Failure("PAYOUT_BATCH_NOT_OPEN", $"Batch is {batch.Status}");

        var now = _dateTime.Now;
        var actor = _currentUser.UserId;
        var pending = await _db.PayoutAttempts
            .Where(a => a.PayoutBatchId == batchId && a.Outcome == PayoutOutcome.Pending)
            .ToListAsync(ct);

        foreach (var a in pending)
        {
            // Setting Outcome = Failed frees reserved earnings via the reservation guard.
            a.Outcome = PayoutOutcome.Failed;
            a.GatewayErrorMessage = "Batch cancelled";
            a.CompletedAtUtc = now;
            a.LastUpdateDate = now;
            a.LastUpdateBy = actor;
        }

        batch.Status = PayoutBatchStatus.Cancelled;
        batch.Notes = "Cancelled by admin";
        batch.LastUpdateDate = now;
        batch.LastUpdateBy = actor;
        await _db.SaveChangesAsync(ct);

        return Result<BatchReconcileResult>.Success(new BatchReconcileResult(0, pending.Count, batch.Status));
    }
}
