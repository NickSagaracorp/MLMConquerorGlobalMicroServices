using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RankRequirements;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.GetRankDashboard;

public record GetRankDashboardQuery : IRequest<Result<RankDashboardDto>>;
