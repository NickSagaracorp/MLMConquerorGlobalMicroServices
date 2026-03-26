using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.GetRankRequirements;

public record GetRankRequirementsQuery(int RankId) : IRequest<Result<IEnumerable<RankRequirementDto>>>;
