using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionCollection;

/// <summary>
/// Returns all CommissionEarnings whose PeriodDate falls in the given period.
/// collectionId format: "YYYY-MM-DD" (exact date) or "YYYY-MM" (full month).
/// </summary>
public record GetCommissionCollectionQuery(
    string CollectionId,
    int Page = 1,
    int PageSize = 50
) : IRequest<Result<PagedResult<CommissionEarningResponse>>>;
