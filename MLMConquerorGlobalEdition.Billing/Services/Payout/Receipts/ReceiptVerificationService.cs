using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public class ReceiptVerificationService : IReceiptVerificationService
{
    private readonly AppDbContext _db;
    private readonly IReceiptStorage _storage;

    public ReceiptVerificationService(AppDbContext db, IReceiptStorage storage)
    {
        _db = db; _storage = storage;
    }

    public async Task<ReceiptVerificationResult> VerifyAsync(PayoutAttempt attempt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(attempt.ReceiptUrl) || string.IsNullOrEmpty(attempt.ReceiptSha256))
            return new ReceiptVerificationResult(false, false, false, false, null, "No receipt issued for this payout.");

        // Recompute the PDF hash.
        var fileName = PayoutReceiptFileNaming.Build(attempt.Id, attempt.MemberId);
        var bytes = await _storage.ReadAsync(fileName, ct);
        if (bytes is null)
            return new ReceiptVerificationResult(true, false, false, attempt.ReceiptAnchorRef != null,
                attempt.ReceiptAnchorRef, "Receipt file not found in storage.");

        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var hashMatches = string.Equals(actualHash, attempt.ReceiptSha256, StringComparison.OrdinalIgnoreCase);

        // Re-walk the chain link: ChainHash(prevEntry.PrevHash | Genesis, thisSha256) == thisPrevHash.
        var chainValid = false;
        if (attempt.ReceiptLedgerSeq is long seq && attempt.ReceiptPrevHash is not null)
        {
            var prevChain = MerkleTree.Genesis;
            if (seq > 1)
            {
                prevChain = await _db.PayoutAttempts
                    .Where(a => a.ReceiptLedgerSeq == seq - 1)
                    .Select(a => a.ReceiptPrevHash)
                    .FirstOrDefaultAsync(ct) ?? MerkleTree.Genesis;
            }
            chainValid = string.Equals(
                MerkleTree.ChainHash(prevChain, attempt.ReceiptSha256), attempt.ReceiptPrevHash,
                StringComparison.OrdinalIgnoreCase);
        }

        var anchored = !string.IsNullOrEmpty(attempt.ReceiptAnchorRef);
        var detail = hashMatches
            ? (chainValid ? "Receipt authentic; chain link verified." : "Hash OK; not yet chained/anchored.")
            : "TAMPERED: stored PDF hash does not match the recorded hash.";

        return new ReceiptVerificationResult(true, hashMatches, chainValid, anchored, attempt.ReceiptAnchorRef, detail);
    }
}
