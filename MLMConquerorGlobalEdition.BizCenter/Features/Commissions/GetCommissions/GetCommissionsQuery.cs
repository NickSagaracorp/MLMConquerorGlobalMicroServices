using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissions;

/// <summary>
/// Optional <paramref name="Status"/> accepts "Pending", "Paid", or "Cancelled".
/// Optional <paramref name="From"/> / <paramref name="To"/> filter by EarnedDate range (inclusive).
/// </summary>
public record GetCommissionsQuery(
    int       Page,
    int       PageSize,
    string?   Status = null,
    DateTime? From   = null,
    DateTime? To     = null)
    : IRequest<Result<PagedResult<CommissionEarningDto>>>;
