using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.UpdateRankRequirement;

public record UpdateRankRequirementCommand(int RankId, int Id, CreateRankRequirementDto Request)
    : IRequest<Result<bool>>;
