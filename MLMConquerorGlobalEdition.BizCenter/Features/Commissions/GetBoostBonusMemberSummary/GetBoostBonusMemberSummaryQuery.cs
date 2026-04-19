using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusMemberSummary;

public record GetBoostBonusMemberSummaryQuery : IRequest<Result<BoostBonusMemberSummaryDto>>;
