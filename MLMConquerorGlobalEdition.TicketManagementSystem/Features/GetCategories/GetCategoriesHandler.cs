using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetCategories;

public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, Result<IEnumerable<TicketCategoryDto>>>
{
    private readonly AppDbContext _db;

    public GetCategoriesHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IEnumerable<TicketCategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var categories = await _db.TicketCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new TicketCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive
            })
            .ToListAsync(ct);

        return Result<IEnumerable<TicketCategoryDto>>.Success(categories);
    }
}
