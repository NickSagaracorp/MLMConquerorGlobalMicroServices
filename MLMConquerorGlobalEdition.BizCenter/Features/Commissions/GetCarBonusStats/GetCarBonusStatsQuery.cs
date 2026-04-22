using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusStats;

public record GetCarBonusStatsQuery : IRequest<Result<CarBonusStatsDto>>;
