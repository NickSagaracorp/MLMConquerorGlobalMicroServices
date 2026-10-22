using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Loyalty;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;

public class GetLoyaltyPointsHandler : IRequestHandler<GetLoyaltyPointsQuery, Result<PagedResult<LoyaltyPointsDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetLoyaltyPointsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<LoyaltyPointsDto>>> Handle(GetLoyaltyPointsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var query = _db.LoyaltyPoints
            .AsNoTracking()
            .Where(lp => lp.MemberId == memberId)
            .OrderByDescending(lp => lp.YearNo)
            .ThenByDescending(lp => lp.MonthNo);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(lp => new LoyaltyPointsDto
            {
                Id = lp.Id,
                ProductId = lp.ProductId,
                OrderId = lp.OrderId,
                PointsEarned = lp.PointsEarned,
                IsLocked = lp.IsLocked,
                MissedPayment = lp.MissedPayment,
                NumberOfSuccessPayments = lp.NumberOfSuccessPayments,
                MonthNo = lp.MonthNo,
                YearNo = lp.YearNo,
                UnlockedAt = lp.UnlockedAt
            })
            .ToListAsync(ct);

        var result = new PagedResult<LoyaltyPointsDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<LoyaltyPointsDto>>.Success(result);
    }
}
