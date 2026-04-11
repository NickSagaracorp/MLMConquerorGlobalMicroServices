using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Mappings;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetRankDefinitions;

public class GetRankDefinitionsHandler : IRequestHandler<GetRankDefinitionsQuery, Result<List<RankDefinitionResponse>>>
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public GetRankDefinitionsHandler(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<List<RankDefinitionResponse>>> Handle(GetRankDefinitionsQuery request, CancellationToken ct)
    {
        var cached = await _cache.GetAsync<List<RankDefinitionResponse>>(CacheKeys.RankDefinitions, ct);
        if (cached is not null)
            return Result<List<RankDefinitionResponse>>.Success(cached);

        var definitions = await _db.RankDefinitions
            .AsNoTracking()
            .Include(r => r.Requirements)
            .Where(r => r.Status == RankDefinitionStatus.Active)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var result = definitions.Select(d => d.ToResponse()).ToList();
        await _cache.SetAsync(CacheKeys.RankDefinitions, result, CacheKeys.RankDefinitionsTtl, ct);

        return Result<List<RankDefinitionResponse>>.Success(result);
    }
}
