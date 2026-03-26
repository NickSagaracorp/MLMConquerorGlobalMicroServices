using MediatR;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;

public record GenerateCertificateCommand(string MemberRankHistoryId) : IRequest<Result<CertificateGenerationResponse>>;
