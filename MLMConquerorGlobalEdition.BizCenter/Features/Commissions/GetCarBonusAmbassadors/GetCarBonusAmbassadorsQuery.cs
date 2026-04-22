using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusAmbassadors;

public record GetCarBonusAmbassadorsQuery(
    DateTime? From = null,
    DateTime? To   = null
) : IRequest<Result<List<CarBonusAmbassadorDto>>>;
