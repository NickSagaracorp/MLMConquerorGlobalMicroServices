using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>
/// Thin facade over the RankEngine HTTP API. BizCenter does not reference
/// RankEngine directly (different deployment target, different DbContext scope),
/// so we proxy a tiny number of calls — currently just on-demand certificate
/// generation — through this client.
/// </summary>
public interface IRankEngineClient
{
    /// <summary>
    /// Triggers certificate generation for the supplied rank history record on the
    /// MEMBER'S behalf — the caller's JWT is relayed and RankEngine enforces
    /// ownership (the JWT's memberId must match the record's MemberId).
    /// Returns the generated certificate URL on success.
    /// </summary>
    Task<Result<string>> GenerateMemberCertificateAsync(
        string memberRankHistoryId, string bearerToken, CancellationToken ct = default);
}
