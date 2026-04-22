using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeStats;

public record GetDualTreeStatsQuery(string NodeMemberId)
    : IRequest<Result<DualTreeStatsDto>>;
