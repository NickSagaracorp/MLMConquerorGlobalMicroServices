using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentCustomers;

/// <summary>BizCenter "customers" endpoint — delegates to <see cref="IEnrollmentTeamService"/>.</summary>
public class GetEnrollmentCustomersHandler
    : IRequestHandler<GetEnrollmentCustomersQuery, Result<PagedResult<EnrollmentCustomerDto>>>
{
    private readonly IEnrollmentTeamService _service;
    private readonly ICurrentUserService    _currentUser;

    public GetEnrollmentCustomersHandler(IEnrollmentTeamService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<EnrollmentCustomerDto>>> Handle(
        GetEnrollmentCustomersQuery request, CancellationToken ct)
    {
        var view = await _service.GetCustomersAsync(
            _currentUser.MemberId, request.Page, request.PageSize, request.Search, ct);

        return Result<PagedResult<EnrollmentCustomerDto>>.Success(new PagedResult<EnrollmentCustomerDto>
        {
            TotalCount = view.TotalCount,
            Page       = view.Page,
            PageSize   = view.PageSize,
            Items      = view.Items.Select(c => new EnrollmentCustomerDto
            {
                MemberId         = c.MemberId,
                FullName         = c.FullName,
                Email            = c.Email,
                Phone            = c.Phone,
                Country          = c.Country,
                EnrollDate       = c.EnrollDate,
                SponsorMemberId  = c.SponsorMemberId,
                SponsorFullName  = c.SponsorFullName,
                AccountStatus    = c.AccountStatus,
                MembershipStatus = c.MembershipStatus,
                MembershipLevel  = c.MembershipLevel,
                PersonalPoints   = c.PersonalPoints
            }).ToList()
        });
    }
}
