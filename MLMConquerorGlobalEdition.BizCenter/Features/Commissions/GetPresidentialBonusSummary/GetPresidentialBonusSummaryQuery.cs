using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusSummary;

public record GetPresidentialBonusSummaryQuery : IRequest<Result<CommissionBonusSummaryDto>>;
