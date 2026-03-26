using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetRankDefinitions;

public class GetRankDefinitionsHandler : IRequestHandler<GetRankDefinitionsQuery, Result<List<RankDefinitionResponse>>>
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public GetRankDefinitionsHandler(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<Result<List<RankDefinitionResponse>>> Handle(GetRankDefinitionsQuery request, CancellationToken ct)
    {
        var definitions = await _db.RankDefinitions
            .AsNoTracking()
            .Include(r => r.Requirements)
            .Where(r => r.Status == RankDefinitionStatus.Active)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var result = _mapper.Map<List<RankDefinitionResponse>>(definitions);
        return Result<List<RankDefinitionResponse>>.Success(result);
    }
}
