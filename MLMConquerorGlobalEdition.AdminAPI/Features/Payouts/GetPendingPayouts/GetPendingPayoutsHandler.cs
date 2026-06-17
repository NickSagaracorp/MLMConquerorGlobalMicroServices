using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPendingPayouts;

public class GetPendingPayoutsHandler
    : IRequestHandler<GetPendingPayoutsQuery, Result<PagedResult<PendingPayoutRowDto>>>
{
    private readonly AppDbContext _db;
    public GetPendingPayoutsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<PendingPayoutRowDto>>> Handle(
        GetPendingPayoutsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        // Earnings already reserved by a non-failed attempt (matches the orchestrator guard).
        var reservedEarningIds = _db.PayoutAttemptEarnings
            .Where(pae => _db.PayoutAttempts.Any(a => a.Id == pae.PayoutAttemptId && a.Outcome != PayoutOutcome.Failed))
            .Select(pae => pae.CommissionEarningId);

        // Per-member pending total, due on/before processDate, not reserved (DB-side aggregation).
        var perMember = _db.CommissionEarnings
            .Where(e => e.Status == CommissionEarningStatus.Pending
                        && e.PaymentDate <= request.ProcessDate
                        && !e.IsDeleted
                        && (request.CommissionTypeId == null || e.CommissionTypeId == request.CommissionTypeId)
                        && !reservedEarningIds.Contains(e.Id))
            .GroupBy(e => e.BeneficiaryMemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.Amount) });

        // Eligible candidates: join preferred approved wallet + active threshold, total >= minimum.
        var candidates =
            from p in perMember
            join w in _db.Wallets.Where(w => w.IsPreferred && w.Status == WalletStatus.Approved && !w.IsDeleted)
                on p.MemberId equals w.MemberId
            join s in _db.PaymentGateways.Where(g => g.IsActive)
                on w.WalletType equals s.WalletType
            where p.Total >= s.MinimumPayoutAmount
                  && (request.WalletType == null || w.WalletType == request.WalletType)
            select new { p.MemberId, p.Total, w.WalletType };

        var totalCount = await candidates.CountAsync(ct);

        var pageRows = await candidates
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.MemberId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = pageRows.Select(r => r.MemberId).ToList();

        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(m => ids.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = (m.FirstName + " " + m.LastName).Trim() })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        // Last attempt per page member (page-scoped, grouped in memory over <= pageSize members).
        var attempts = await _db.PayoutAttempts.AsNoTracking()
            .Where(a => ids.Contains(a.MemberId))
            .Select(a => new { a.MemberId, a.AttemptedAtUtc, a.Outcome, a.GatewayErrorMessage })
            .ToListAsync(ct);
        var lastByMember = attempts
            .GroupBy(a => a.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttemptedAtUtc).First());

        var items = pageRows.Select(r =>
        {
            lastByMember.TryGetValue(r.MemberId, out var last);
            return new PendingPayoutRowDto
            {
                MemberId = r.MemberId,
                FullName = names.TryGetValue(r.MemberId, out var n) ? n : string.Empty,
                PendingAmount = r.Total,
                WalletType = r.WalletType,
                LastAttemptOutcome = last?.Outcome,
                LastAttemptError = last?.GatewayErrorMessage
            };
        }).ToList();

        return Result<PagedResult<PendingPayoutRowDto>>.Success(new PagedResult<PendingPayoutRowDto>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        });
    }
}
