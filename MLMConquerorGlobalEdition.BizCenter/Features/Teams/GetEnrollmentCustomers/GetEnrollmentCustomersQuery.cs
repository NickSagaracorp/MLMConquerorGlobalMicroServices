using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentCustomers;

public record GetEnrollmentCustomersQuery(
    int     Page     = 1,
    int     PageSize = 20,
    string? Search   = null
) : IRequest<Result<PagedResult<EnrollmentCustomerDto>>>;
