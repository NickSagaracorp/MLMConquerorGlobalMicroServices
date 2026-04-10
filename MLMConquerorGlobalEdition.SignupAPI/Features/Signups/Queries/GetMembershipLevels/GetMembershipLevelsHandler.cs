using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Mappings;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetMembershipLevels;

public class GetMembershipLevelsHandler : IRequestHandler<GetMembershipLevelsQuery, Result<IEnumerable<MembershipLevelDto>>>
{
    private readonly AppDbContext _db;

    public GetMembershipLevelsHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<MembershipLevelDto>>> Handle(GetMembershipLevelsQuery query, CancellationToken ct)
    {
        var levels = await _db.MembershipLevels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        return Result<IEnumerable<MembershipLevelDto>>.Success(levels.Select(l => l.ToDto()));
    }
}
