using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetBillingHistory;

public class GetBillingHistoryHandler : IRequestHandler<GetBillingHistoryQuery, Result<PagedResult<OrderHistoryDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBillingHistoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<OrderHistoryDto>>> Handle(GetBillingHistoryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var query = _db.Orders
            .AsNoTracking()
            .Where(o => o.MemberId == memberId)
            .OrderByDescending(o => o.OrderDate);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OrderHistoryDto
            {
                OrderId = o.Id,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                Notes = o.Notes
            })
            .ToListAsync(ct);

        var result = new PagedResult<OrderHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<OrderHistoryDto>>.Success(result);
    }
}
