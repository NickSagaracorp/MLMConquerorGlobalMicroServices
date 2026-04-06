using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Dashboard;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Dashboard.GetDashboardStats;

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardStatsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<DashboardStatsDto>> Handle(
        GetDashboardStatsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Total earnings — sum of all paid commissions
        var totalEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.Status == CommissionEarningStatus.Paid)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        // Team size — direct children in enrollment tree
        var teamSize = await _db.GenealogyTree
            .AsNoTracking()
            .CountAsync(g => g.ParentMemberId == memberId, ct);

        // Token balance — aggregate across all token types
        var tokenBalance = await _db.TokenBalances
            .AsNoTracking()
            .Where(tb => tb.MemberId == memberId)
            .SumAsync(tb => (int?)tb.Balance, ct) ?? 0;

        // Current rank — most recently achieved rank
        var currentRank = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt)
            .Select(r => r.RankDefinition!.Name)
            .FirstOrDefaultAsync(ct);

        // Fast Start Bonus windows — pulled from the FSB commission type earnings
        // Commission type with category name containing "Fast Start" is filtered by type
        var fsbEarnings = await _db.CommissionEarnings
            .AsNoTracking()
            .Include(c => c.CommissionType)
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.CommissionType != null
                     && c.CommissionType.Name.Contains("Fast Start"))
            .OrderBy(c => c.EarnedDate)
            .ToListAsync(ct);

        // Group into 3 windows (each window = one FSB order event)
        var fsbWindows = fsbEarnings
            .Select((e, i) => new FsbWindowDto
            {
                WindowNumber = i + 1,
                StartDate    = e.EarnedDate,
                EndDate      = e.PaymentDate,
                Earned       = e.Amount,
                Status       = e.Status == CommissionEarningStatus.Paid ? "Paid"
                             : e.Status == CommissionEarningStatus.Cancelled ? "Cancelled"
                             : "Pending"
            })
            .Take(3)
            .ToList();

        var dto = new DashboardStatsDto
        {
            TotalEarnings = totalEarnings,
            TeamSize      = teamSize,
            TokenBalance  = tokenBalance,
            CurrentRank   = currentRank,
            FsbWindows    = fsbWindows
        };

        return Result<DashboardStatsDto>.Success(dto);
    }
}
