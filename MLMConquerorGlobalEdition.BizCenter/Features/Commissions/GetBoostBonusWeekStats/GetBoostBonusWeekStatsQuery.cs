using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusWeekStats;

public record GetBoostBonusWeekStatsQuery : IRequest<Result<BoostBonusWeekStatsDto>>;
