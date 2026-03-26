using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankDashboard;

public record GetRankDashboardQuery() : IRequest<Result<RankDashboardDto>>;
