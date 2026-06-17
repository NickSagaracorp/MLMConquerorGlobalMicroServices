using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;

/// <summary>Simulated external anchor. Returns a deterministic reference derived from the root so the
/// audit trail is wired end-to-end; replace with a real chain provider when credentials exist.</summary>
public class StubDocumentAnchorService : IDocumentAnchorService
{
    public Task<Result<string>> AnchorAsync(string merkleRootHex, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success($"sim-anchor:{merkleRootHex[..Math.Min(16, merkleRootHex.Length)]}"));
}
