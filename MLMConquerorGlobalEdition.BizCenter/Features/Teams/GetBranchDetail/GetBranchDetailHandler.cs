using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetBranchDetail;

/// <summary>
/// BizCenter "branch detail" endpoint. Performs the BizCenter-only ownership
/// check (the branch must be a direct child of the current member), then
/// delegates the data computation to <see cref="IEnrollmentTeamService"/>.
/// </summary>
public class GetBranchDetailHandler : IRequestHandler<GetBranchDetailQuery, Result<BranchDetailDto>>
{
    private readonly AppDbContext           _db;
    private readonly IEnrollmentTeamService _service;
    private readonly ICurrentUserService    _currentUser;

    public GetBranchDetailHandler(
        AppDbContext db,
        IEnrollmentTeamService service,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<BranchDetailDto>> Handle(GetBranchDetailQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Ownership check — only direct children of the current member are valid branches.
        var isMyDirectChild = await _db.GenealogyTree.AsNoTracking()
            .AnyAsync(g => g.MemberId == request.BranchMemberId
                        && g.ParentMemberId == memberId, ct);
        if (!isMyDirectChild)
            return Result<BranchDetailDto>.Failure(
                "FORBIDDEN", "Branch is not a direct sponsored ambassador.");

        var detail = await _service.GetBranchDetailAsync(request.BranchMemberId, ct);
        if (detail is null)
            return Result<BranchDetailDto>.Failure("BRANCH_NOT_FOUND", "Branch not found.");

        return Result<BranchDetailDto>.Success(new BranchDetailDto
        {
            BranchMemberId   = detail.BranchMemberId,
            BranchMemberName = detail.BranchMemberName,
            TotalPoints      = detail.TotalPoints,
            Ambassadors = detail.Ambassadors.Select(a => new BranchAmbassadorItemDto
            {
                SeqNo               = a.SeqNo,
                Level               = a.Level,
                FullName            = a.FullName,
                AccountStatus       = a.AccountStatus,
                MembershipStatus    = a.MembershipStatus,
                IsQualified         = a.IsQualified,
                MembershipLevelName = a.MembershipLevelName,
                EnrollmentPoints    = a.EnrollmentPoints
            }).ToList(),
            Customers = detail.Customers.Select(c => new BranchCustomerItemDto
            {
                SeqNo               = c.SeqNo,
                Level               = c.Level,
                FullName            = c.FullName,
                MembershipStatus    = c.MembershipStatus,
                MembershipLevelName = c.MembershipLevelName,
                EnrollmentPoints    = c.EnrollmentPoints
            }).ToList()
        });
    }
}
