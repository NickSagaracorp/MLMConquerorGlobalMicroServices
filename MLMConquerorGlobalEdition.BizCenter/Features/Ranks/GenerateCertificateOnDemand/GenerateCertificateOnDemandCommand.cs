using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GenerateCertificateOnDemand;

/// <summary>
/// On-demand certificate generation triggered by the MEMBER from BizCenter.
/// The bearer token is captured at the controller boundary and relayed to
/// RankEngine so it can enforce the same ownership check as a direct API call.
/// </summary>
public record GenerateCertificateOnDemandCommand(string RankHistoryId, string BearerToken)
    : IRequest<Result<string>>;
