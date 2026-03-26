using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.CreateRankRequirement;

public record CreateRankRequirementCommand(int RankId, CreateRankRequirementDto Request)
    : IRequest<Result<int>>;
