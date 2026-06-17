using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>Daily: builds the internal Merkle hash-chain over receipts that have a SHA-256 but no ledger
/// sequence yet, then anchors the batch's Merkle root externally. Single-threaded → no ledger-sequence race.
/// Idempotent: re-running only picks up still-unchained receipts.</summary>
[Queue("billing")]
public class ReceiptAnchorJob
{
    private readonly AppDbContext _db;
    private readonly IDocumentAnchorService _anchor;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ReceiptAnchorJob> _logger;

    public ReceiptAnchorJob(AppDbContext db, IDocumentAnchorService anchor, IDateTimeProvider dateTime,
        ILogger<ReceiptAnchorJob> logger)
    {
        _db = db; _anchor = anchor; _dateTime = dateTime; _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Receipts issued but not yet chained, in deterministic order.
        var pending = await _db.PayoutAttempts
            .Where(a => a.Outcome == PayoutOutcome.Success
                        && a.ReceiptSha256 != null
                        && a.ReceiptLedgerSeq == null)
            .OrderBy(a => a.CompletedAtUtc).ThenBy(a => a.Id)
            .ToListAsync(ct);

        if (pending.Count == 0) { _logger.LogInformation("ReceiptAnchorJob: nothing to chain."); return; }

        // Chain head = the highest existing ledger entry.
        var head = await _db.PayoutAttempts
            .Where(a => a.ReceiptLedgerSeq != null)
            .OrderByDescending(a => a.ReceiptLedgerSeq)
            .Select(a => new { a.ReceiptLedgerSeq, a.ReceiptPrevHash })
            .FirstOrDefaultAsync(ct);

        var seq = head?.ReceiptLedgerSeq ?? 0;
        var prevChain = head?.ReceiptPrevHash ?? MerkleTree.Genesis;

        var leafHashes = new List<string>();
        foreach (var a in pending)
        {
            seq++;
            prevChain = MerkleTree.ChainHash(prevChain, a.ReceiptSha256!);
            a.ReceiptLedgerSeq = seq;
            a.ReceiptPrevHash = prevChain;
            leafHashes.Add(a.ReceiptSha256!);
        }

        var root = MerkleTree.ComputeRoot(leafHashes);
        var anchorResult = await _anchor.AnchorAsync(root, ct);
        var anchorRef = anchorResult.IsSuccess ? anchorResult.Value! : null;

        var now = _dateTime.Now;
        foreach (var a in pending)
        {
            a.ReceiptAnchorRef = anchorRef;
            a.LastUpdateDate = now;
            a.LastUpdateBy = "receipt-anchor-job";
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("ReceiptAnchorJob: chained {Count} receipts, root {Root}, anchor {Ref}.",
            pending.Count, root, anchorRef);
    }
}
