using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicketCategories;

public class GetTicketCategoriesHandler
    : IRequestHandler<GetTicketCategoriesQuery, Result<IEnumerable<TicketCategoryDto>>>
{
    private readonly AppDbContext _db;

    public GetTicketCategoriesHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<TicketCategoryDto>>> Handle(
        GetTicketCategoriesQuery request, CancellationToken ct)
    {
        var items = await _db.TicketCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new TicketCategoryDto { Id = c.Id, Name = c.Name })
            .ToListAsync(ct);

        return Result<IEnumerable<TicketCategoryDto>>.Success(items);
    }
}
