using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusSummary;

public record GetFastStartBonusSummaryQuery : IRequest<Result<CommissionBonusSummaryDto>>;
