using System.Text;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;

public class PayoutBatchExportService : IPayoutBatchExportService
{
    private readonly AppDbContext _db;
    private readonly IPayoutCsvResolver _csv;
    private readonly IReceiptStorage _storage;
    private readonly Services.IDateTimeProvider _dateTime;
    private readonly Services.ICurrentUserService _currentUser;
    private readonly IMemberFundsGate _fundsGate;

    /// <summary>
    /// Espera por miembro dentro del lote. Corta a propósito: un miembro que está comprando no debe
    /// retener al resto; queda para el próximo lote.
    /// </summary>
    private static readonly TimeSpan MemberLockTimeout = TimeSpan.FromSeconds(2);

    public PayoutBatchExportService(
        AppDbContext db,
        IPayoutCsvResolver csv,
        IReceiptStorage storage,
        Services.IDateTimeProvider dateTime,
        Services.ICurrentUserService currentUser,
        IMemberFundsGate fundsGate)
    {
        _db = db;
        _csv = csv;
        _storage = storage;
        _dateTime = dateTime;
        _currentUser = currentUser;
        _fundsGate = fundsGate;
    }

    public async Task<Result<PayoutBatchExportResult>> ExportAsync(WalletType walletType, DateTime processDate, CancellationToken ct = default)
    {
        var formatterResult = _csv.ResolveFormatter(walletType);
        if (!formatterResult.IsSuccess)
            return Result<PayoutBatchExportResult>.Failure(formatterResult.ErrorCode!, formatterResult.Error!);

        var now = _dateTime.Now;
        var actor = _currentUser.UserId;

        // Eligible candidate members for this gateway (reservation-aware), with their account.
        var reservedEarningIds = _db.PayoutAttemptEarnings
            .Where(pae => _db.PayoutAttempts.Any(a => a.Id == pae.PayoutAttemptId && a.Outcome != PayoutOutcome.Failed))
            .Select(pae => pae.CommissionEarningId);

        var perMember = _db.CommissionEarnings
            .Where(e => e.Status == CommissionEarningStatus.Pending && (e.Amount < 0 || e.PaymentDate <= processDate)
                        && !e.IsDeleted && !reservedEarningIds.Contains(e.Id))
            .GroupBy(e => e.BeneficiaryMemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.Amount) });

        var candidates = await (
            from p in perMember
            join w in _db.Wallets.Where(w => w.IsPreferred && w.Status == WalletStatus.Approved && !w.IsDeleted)
                on p.MemberId equals w.MemberId
            join s in _db.PaymentGateways.Where(g => g.IsActive)
                on w.WalletType equals s.WalletType
            where w.WalletType == walletType && p.Total >= s.MinimumPayoutAmount
            select new { p.MemberId, p.Total, Account = w.AccountIdentifier, Minimum = s.MinimumPayoutAmount })
            .ToListAsync(ct);

        var batch = new PayoutBatch
        {
            WalletType = walletType,
            ProcessDateUtc = processDate,
            Status = PayoutBatchStatus.Exported,
            MemberCount = 0,
            TotalAmountUsd = 0m,
            CreationDate = now,
            CreatedBy = actor,
            LastUpdateDate = now,
            LastUpdateBy = actor
        };

        if (candidates.Count == 0)
        {
            // Nothing to export — persist an empty batch for the audit trail and return an empty CSV.
            _db.PayoutBatches.Add(batch);
            await _db.SaveChangesAsync(ct);
            var emptyCsv = formatterResult.Value!.FormatExport(Array.Empty<PayoutCsvRow>());
            return Result<PayoutBatchExportResult>.Success(new PayoutBatchExportResult(
                batch.Id, 0, 0m, Encoding.UTF8.GetBytes(emptyCsv), $"payout-batch-{batch.Id}.csv"));
        }

        // Orden ordinal fijo: el lote toma varios candados de fondos y dos lotes simultáneos los
        // piden en el mismo orden, así que no pueden interbloquearse. Las compras y los pagos sueltos
        // toman uno solo cada uno.
        var candidateIds = candidates.Select(c => c.MemberId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
        var candidateById = candidates.GroupBy(c => c.MemberId).ToDictionary(g => g.Key, g => g.First());

        var csvRows = new List<PayoutCsvRow>();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Candado de fondos por miembro antes de leer lo que se reserva: una compra de tokens con
            // comisiones toma el mismo candado, así que no puede gastar lo que este lote reserva. Un
            // miembro ocupado no frena el lote: queda fuera y entra en el siguiente.
            var lockedIds = new List<string>(candidateIds.Count);
            foreach (var memberId in candidateIds)
            {
                if (await _fundsGate.TryAcquireAsync(memberId, MemberLockTimeout, ct))
                    lockedIds.Add(memberId);
            }

            // Lectura con los candados ya tomados: es la que manda.
            var reservedAtQuery = _db.PayoutAttemptEarnings
                .Where(pae => _db.PayoutAttempts.Any(a => a.Id == pae.PayoutAttemptId && a.Outcome != PayoutOutcome.Failed))
                .Select(pae => pae.CommissionEarningId);

            var earnings = await _db.CommissionEarnings
                .Where(e => lockedIds.Contains(e.BeneficiaryMemberId)
                            && e.Status == CommissionEarningStatus.Pending && (e.Amount < 0 || e.PaymentDate <= processDate)
                            && !e.IsDeleted && !reservedAtQuery.Contains(e.Id))
                .Select(e => new { e.Id, e.BeneficiaryMemberId, e.Amount })
                .ToListAsync(ct);

            var earningsByMember = earnings.GroupBy(e => e.BeneficiaryMemberId)
                .ToDictionary(g => g.Key, g => g.ToList());

            _db.PayoutBatches.Add(batch);
            await _db.SaveChangesAsync(ct); // get batch.Id

            decimal batchTotal = 0m;
            var count = 0;
            foreach (var memberId in lockedIds)
            {
                var c = candidateById[memberId];
                if (!earningsByMember.TryGetValue(c.MemberId, out var memberEarnings) || memberEarnings.Count == 0)
                    continue;

                var total = memberEarnings.Sum(e => e.Amount);

                // Releído bajo el candado: si entretanto una compra de tokens dejó su débito, el total
                // puede haber bajado del mínimo de la pasarela (o a cero). Entonces este lote no lo paga.
                if (total <= 0m || total < c.Minimum)
                    continue;

                var attempt = new PayoutAttempt
                {
                    MemberId = c.MemberId,
                    WalletTypeSnapshot = walletType,
                    PayoutAccountSnapshot = c.Account ?? string.Empty,
                    AmountUsd = total,
                    ProcessDateUtc = processDate,
                    Outcome = PayoutOutcome.Pending,
                    AttemptedAtUtc = now,
                    EarningsCount = memberEarnings.Count,
                    DisbursementMode = DisbursementMode.CsvBulk,
                    PayoutBatchId = batch.Id,
                    CreationDate = now,
                    CreatedBy = actor
                };
                _db.PayoutAttempts.Add(attempt);
                await _db.SaveChangesAsync(ct); // get attempt.Id

                foreach (var e in memberEarnings)
                    _db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
                    {
                        PayoutAttemptId = attempt.Id,
                        CommissionEarningId = e.Id,
                        Amount = e.Amount,
                        CreationDate = now,
                        CreatedBy = actor
                    });

                csvRows.Add(new PayoutCsvRow(attempt.Id, c.MemberId, c.Account ?? string.Empty, total));
                batchTotal += total;
                count++;
            }

            batch.MemberCount = count;
            batch.TotalAmountUsd = batchTotal;
            batch.LastUpdateDate = now;
            batch.LastUpdateBy = actor;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        var csv = formatterResult.Value!.FormatExport(csvRows);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var fileName = $"payout-batch-{batch.Id}.csv";
        batch.ExportCsvUrl = await _storage.SaveAsync(fileName, bytes, ct);
        batch.LastUpdateDate = now;
        batch.LastUpdateBy = actor;
        await _db.SaveChangesAsync(ct);

        return Result<PayoutBatchExportResult>.Success(new PayoutBatchExportResult(
            batch.Id, batch.MemberCount, batch.TotalAmountUsd, bytes, fileName));
    }
}
