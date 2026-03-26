using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetHealthDashboard;

public class GetHealthDashboardHandler : IRequestHandler<GetHealthDashboardQuery, Result<HealthDashboardDto>>
{
    private readonly AppDbContext _db;

    public GetHealthDashboardHandler(AppDbContext db) => _db = db;

    public async Task<Result<HealthDashboardDto>> Handle(
        GetHealthDashboardQuery request, CancellationToken cancellationToken)
    {
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

        return Result<HealthDashboardDto>.Success(dto);
    }
}
