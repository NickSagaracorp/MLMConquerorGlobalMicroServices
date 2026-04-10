using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Mappings;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetRankDefinitions;

public class GetRankDefinitionsHandler : IRequestHandler<GetRankDefinitionsQuery, Result<List<RankDefinitionResponse>>>
{
    private readonly AppDbContext _db;

    public GetRankDefinitionsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<RankDefinitionResponse>>> Handle(GetRankDefinitionsQuery request, CancellationToken ct)
    {
        var definitions = await _db.RankDefinitions
            .AsNoTracking()
            .Include(r => r.Requirements)
            .Where(r => r.Status == RankDefinitionStatus.Active)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        return Result<List<RankDefinitionResponse>>.Success(definitions.Select(d => d.ToResponse()).ToList());
    }
}
