using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusCommissions;

public record GetCarBonusCommissionsQuery(
    int       Page,
    int       PageSize,
    DateTime? From = null,
    DateTime? To   = null)
    : IRequest<Result<PagedResult<CommissionEarningDto>>>;
