using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeStats;

public class GetDualTreeStatsHandler : IRequestHandler<GetDualTreeStatsQuery, Result<DualTreeStatsDto>>
{
    private readonly AppDbContext _db;

    public GetDualTreeStatsHandler(AppDbContext db) => _db = db;

    public async Task<Result<DualTreeStatsDto>> Handle(GetDualTreeStatsQuery request, CancellationToken ct)
    {
        var node = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == request.NodeMemberId, ct);

        var dto = new DualTreeStatsDto
        {
            LeftLegPoints  = node?.LeftLegPoints  ?? 0,
            RightLegPoints = node?.RightLegPoints ?? 0
        };

        return Result<DualTreeStatsDto>.Success(dto);
    }
}
