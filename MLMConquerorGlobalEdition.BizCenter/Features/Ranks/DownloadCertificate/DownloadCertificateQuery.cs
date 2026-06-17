using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.DownloadCertificate;

/// <summary>
/// Resolves the certificate URL for the supplied rank history record. If the
/// certificate has not yet been generated, the handler triggers generation
/// (lazy mint) — <see cref="BearerToken"/> is required for that case because
/// the on-demand RankEngine call needs the caller's JWT.
/// </summary>
public record DownloadCertificateQuery(string RankHistoryId, string BearerToken = "")
    : IRequest<Result<string>>;
