using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Mappings;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionRules;

public class GetCommissionRulesHandler : IRequestHandler<GetCommissionRulesQuery, Result<List<CommissionTypeResponse>>>
{
    private readonly AppDbContext _db;

    public GetCommissionRulesHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<CommissionTypeResponse>>> Handle(GetCommissionRulesQuery request, CancellationToken ct)
    {
        var types = await _db.CommissionTypes
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.IsActive)
            .OrderBy(t => t.CommissionCategoryId)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return Result<List<CommissionTypeResponse>>.Success(types.Select(t => t.ToResponse()).ToList());
    }
}
