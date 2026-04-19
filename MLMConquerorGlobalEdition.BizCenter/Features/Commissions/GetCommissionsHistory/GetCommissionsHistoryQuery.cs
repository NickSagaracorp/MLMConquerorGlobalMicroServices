using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsHistory;

public record GetCommissionsHistoryQuery : IRequest<Result<List<CommissionHistoryYearDto>>>;
