using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetMembershipLevels;

public class GetMembershipLevelsHandler : IRequestHandler<GetMembershipLevelsQuery, Result<IEnumerable<MembershipLevelDto>>>
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public GetMembershipLevelsHandler(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<Result<IEnumerable<MembershipLevelDto>>> Handle(GetMembershipLevelsQuery query, CancellationToken ct)
    {
        var levels = await _db.MembershipLevels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        return Result<IEnumerable<MembershipLevelDto>>.Success(_mapper.Map<IEnumerable<MembershipLevelDto>>(levels));
    }
}
