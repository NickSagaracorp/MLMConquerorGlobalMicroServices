using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutStats;

public class GetPayoutStatsHandler : IRequestHandler<GetPayoutStatsQuery, Result<PayoutStatsDto>>
{
    private readonly AppDbContext _db;
    public GetPayoutStatsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PayoutStatsDto>> Handle(GetPayoutStatsQuery request, CancellationToken ct)
    {
        var reservedEarningIds = _db.PayoutAttemptEarnings
            .Where(pae => _db.PayoutAttempts.Any(a => a.Id == pae.PayoutAttemptId && a.Outcome != PayoutOutcome.Failed))
            .Select(pae => pae.CommissionEarningId);

        var perMember = _db.CommissionEarnings
            .Where(e => e.Status == CommissionEarningStatus.Pending
                        && e.PaymentDate <= request.ProcessDate
                        && !e.IsDeleted
                        && (request.CommissionTypeId == null || e.CommissionTypeId == request.CommissionTypeId)
                        && !reservedEarningIds.Contains(e.Id))
            .GroupBy(e => e.BeneficiaryMemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.Amount) });

        // Pending totals per gateway, restricted to eligible candidates (>= threshold).
        var pendingPerGateway = await (
            from p in perMember
            join w in _db.Wallets.Where(w => w.IsPreferred && w.Status == WalletStatus.Approved && !w.IsDeleted)
                on p.MemberId equals w.MemberId
            join s in _db.PaymentGateways.Where(g => g.IsActive)
                on w.WalletType equals s.WalletType
            where p.Total >= s.MinimumPayoutAmount
            group p.Total by w.WalletType into g
            select new { WalletType = g.Key, PendingTotal = g.Sum(), PendingCount = g.Count() }
        ).ToListAsync(ct);

        // Paid totals per gateway for successful attempts completed on the process day.
        var dayStart = request.ProcessDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var paidPerGateway = await _db.PayoutAttempts
            .Where(a => a.Outcome == PayoutOutcome.Success
                        && a.CompletedAtUtc >= dayStart && a.CompletedAtUtc < dayEnd)
            .GroupBy(a => a.WalletTypeSnapshot)
            .Select(g => new { WalletType = g.Key, PaidTotal = g.Sum(x => x.AmountUsd) })
            .ToListAsync(ct);

        var walletTypes = pendingPerGateway.Select(x => x.WalletType)
            .Union(paidPerGateway.Select(x => x.WalletType))
            .Distinct();

        var gateways = walletTypes.Select(wt => new PayoutGatewayStatDto
        {
            WalletType = wt,
            PendingTotal = pendingPerGateway.FirstOrDefault(x => x.WalletType == wt)?.PendingTotal ?? 0m,
            PendingCount = pendingPerGateway.FirstOrDefault(x => x.WalletType == wt)?.PendingCount ?? 0,
            PaidTotal = paidPerGateway.FirstOrDefault(x => x.WalletType == wt)?.PaidTotal ?? 0m
        }).OrderBy(g => g.WalletType).ToList();

        return Result<PayoutStatsDto>.Success(new PayoutStatsDto
        {
            ProcessDate = request.ProcessDate,
            Gateways = gateways
        });
    }
}
