using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionRules;

public class GetCommissionRulesHandler : IRequestHandler<GetCommissionRulesQuery, Result<List<CommissionTypeResponse>>>
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public GetCommissionRulesHandler(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<Result<List<CommissionTypeResponse>>> Handle(GetCommissionRulesQuery request, CancellationToken ct)
    {
        var types = await _db.CommissionTypes
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.IsActive)
            .OrderBy(t => t.CommissionCategoryId)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return Result<List<CommissionTypeResponse>>.Success(_mapper.Map<List<CommissionTypeResponse>>(types));
    }
}
