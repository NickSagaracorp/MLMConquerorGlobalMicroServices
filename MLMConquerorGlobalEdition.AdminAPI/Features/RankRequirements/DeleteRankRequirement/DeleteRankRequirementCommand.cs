using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.DeleteRankRequirement;

public record DeleteRankRequirementCommand(int RankId, int Id) : IRequest<Result<bool>>;
