using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Mappings;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionCollection;

public class GetCommissionCollectionHandler
    : IRequestHandler<GetCommissionCollectionQuery, Result<PagedResult<CommissionEarningResponse>>>
{
    private readonly AppDbContext _db;

    public GetCommissionCollectionHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<CommissionEarningResponse>>> Handle(
        GetCommissionCollectionQuery request, CancellationToken ct)
    {
        // Parse collectionId — supports "YYYY-MM" (month) or "YYYY-MM-DD" (exact date)
        DateTime? exactDate = null;
        int? year = null, month = null;

        if (DateTime.TryParseExact(request.CollectionId, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            exactDate = parsed.Date;
        }
        else if (request.CollectionId.Length == 7 &&
                 int.TryParse(request.CollectionId[..4], out var y) &&
                 int.TryParse(request.CollectionId[5..], out var m))
        {
            year = y; month = m;
        }
        else
        {
            return Result<PagedResult<CommissionEarningResponse>>.Failure(
                "INVALID_COLLECTION_ID",
                "collectionId must be in 'YYYY-MM-DD' or 'YYYY-MM' format.");
        }

        var query = _db.CommissionEarnings
            .AsNoTracking()
            .Include(e => e.CommissionType)
            .Where(e => !e.IsDeleted);

        query = exactDate.HasValue
            ? query.Where(e => e.PeriodDate.HasValue && e.PeriodDate.Value.Date == exactDate.Value)
            : query.Where(e => e.PeriodDate.HasValue
                               && e.PeriodDate.Value.Year == year
                               && e.PeriodDate.Value.Month == month);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.EarnedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var mapped = items.Select(e => e.ToResponse()).ToList();

        return Result<PagedResult<CommissionEarningResponse>>.Success(new PagedResult<CommissionEarningResponse>
        {
            Items = mapped,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
