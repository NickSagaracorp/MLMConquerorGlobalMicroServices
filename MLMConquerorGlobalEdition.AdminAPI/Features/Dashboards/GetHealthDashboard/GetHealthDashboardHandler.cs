using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetHealthDashboard;

/// <summary>
/// Health-check executive dashboard. Cached globally for 3 minutes; pass
/// <c>BypassCache = true</c> for fresh recompute.
/// </summary>
public class GetHealthDashboardHandler : IRequestHandler<GetHealthDashboardQuery, Result<HealthDashboardDto>>
{
    private readonly AppDbContext  _db;
    private readonly ICacheService _cache;

    public GetHealthDashboardHandler(AppDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<Result<HealthDashboardDto>> Handle(
        GetHealthDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<HealthDashboardDto>(CacheKeys.AdminHealthDashboard, cancellationToken);
            if (cached is not null) return Result<HealthDashboardDto>.Success(cached);
        }

        var activeMembers = await _db.MemberProfiles
            .AsNoTracking()
            .CountAsync(m => m.Status == MemberAccountStatus.Active, cancellationToken);

        var pendingPayments = await _db.PaymentHistories
            .AsNoTracking()
            .CountAsync(p => p.TransactionStatus == PaymentHistoryTransactionStatus.Pending, cancellationToken);

        var openTickets = await _db.Tickets
            .AsNoTracking()
            .CountAsync(t => t.Status == TicketStatus.Open, cancellationToken);

        var dto = new HealthDashboardDto
        {
            ActiveMembers = activeMembers,
            PendingPayments = pendingPayments,
            OpenTickets = openTickets,
            Status = "healthy"
        };

        await _cache.SetAsync(CacheKeys.AdminHealthDashboard, dto, CacheKeys.AdminHealthDashboardTtl, cancellationToken);
        return Result<HealthDashboardDto>.Success(dto);
    }
}
