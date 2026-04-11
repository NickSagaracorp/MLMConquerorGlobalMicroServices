using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Mappings;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetMembershipLevels;

public class GetMembershipLevelsHandler : IRequestHandler<GetMembershipLevelsQuery, Result<IEnumerable<MembershipLevelDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public GetMembershipLevelsHandler(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<IEnumerable<MembershipLevelDto>>> Handle(GetMembershipLevelsQuery query, CancellationToken ct)
    {
        var cached = await _cache.GetAsync<List<MembershipLevelDto>>(CacheKeys.MembershipLevels, ct);
        if (cached is not null)
            return Result<IEnumerable<MembershipLevelDto>>.Success(cached);

        var levels = await _db.MembershipLevels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var result = levels.Select(l => l.ToDto()).ToList();
        await _cache.SetAsync(CacheKeys.MembershipLevels, result, CacheKeys.MembershipLevelsTtl, ct);

        return Result<IEnumerable<MembershipLevelDto>>.Success(result);
    }
}
