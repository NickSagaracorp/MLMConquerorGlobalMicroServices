using MediatR;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;

/// <summary>
/// Generates the certificate PDF for a rank achievement.
/// Force = true rebuilds even when a certificate already exists (admin regeneration).
/// </summary>
public record GenerateCertificateCommand(string MemberRankHistoryId, bool Force = false)
    : IRequest<Result<CertificateGenerationResponse>>;
