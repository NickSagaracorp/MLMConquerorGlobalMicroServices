using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;

public interface IDocumentAnchorService
{
    /// <summary>Anchors a Merkle root to an external public ledger and returns the anchor reference.
    /// Stubbed until a real provider (OpenTimestamps/Bitcoin or Polygon) + credentials exist.</summary>
    Task<Result<string>> AnchorAsync(string merkleRootHex, CancellationToken ct = default);
}
