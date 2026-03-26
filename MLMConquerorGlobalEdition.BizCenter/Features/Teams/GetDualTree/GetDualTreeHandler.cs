using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTree;

public class GetDualTreeHandler : IRequestHandler<GetDualTreeQuery, Result<IEnumerable<DualTreeMemberDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDualTreeHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<DualTreeMemberDto>>> Handle(GetDualTreeQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Immediate children (left + right legs) in the dual team tree
        var children = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => d.ParentMemberId == memberId)
            .ToListAsync(ct);

        var childMemberIds = children.Select(c => c.MemberId).ToList();

        var memberProfiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => childMemberIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, ct);

        var result = children
            .Where(c => memberProfiles.ContainsKey(c.MemberId))
            .Select(c => new DualTreeMemberDto
            {
                MemberId = c.MemberId,
                FirstName = memberProfiles[c.MemberId].FirstName,
                LastName = memberProfiles[c.MemberId].LastName,
                Side = c.Side.ToString(),
                MemberType = memberProfiles[c.MemberId].MemberType.ToString(),
                Status = memberProfiles[c.MemberId].Status.ToString(),
                EnrollDate = memberProfiles[c.MemberId].EnrollDate
            })
            .ToList();

        return Result<IEnumerable<DualTreeMemberDto>>.Success(result);
    }
}
