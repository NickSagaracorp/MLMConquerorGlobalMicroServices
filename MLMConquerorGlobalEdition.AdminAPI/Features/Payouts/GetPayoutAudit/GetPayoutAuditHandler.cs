using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAudit;

public class GetPayoutAuditHandler : IRequestHandler<GetPayoutAuditQuery, Result<PagedResult<PayoutAuditRowDto>>>
{
    private readonly AppDbContext _db;

    public GetPayoutAuditHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<PayoutAuditRowDto>>> Handle(GetPayoutAuditQuery r, CancellationToken ct)
    {
        var page = r.Page < 1 ? 1 : r.Page;
        var pageSize = r.PageSize < 1 ? 20 : r.PageSize;

        var q = _db.PayoutAttempts.AsNoTracking().AsQueryable();
        if (r.From is not null) q = q.Where(a => a.AttemptedAtUtc >= r.From);
        if (r.To is not null) q = q.Where(a => a.AttemptedAtUtc < r.To);
        if (!string.IsNullOrWhiteSpace(r.MemberId)) q = q.Where(a => a.MemberId == r.MemberId);
        if (r.WalletType is not null) q = q.Where(a => a.WalletTypeSnapshot == r.WalletType);
        if (!string.IsNullOrWhiteSpace(r.Outcome)) q = q.Where(a => a.Outcome == r.Outcome);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.AttemptedAtUtc).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new PayoutAuditRowDto
            {
                PayoutAttemptId = a.Id,
                MemberId = a.MemberId,
                WalletTypeSnapshot = a.WalletTypeSnapshot,
                PayoutAccountSnapshot = a.PayoutAccountSnapshot,
                AmountUsd = a.AmountUsd,
                Outcome = a.Outcome,
                ProcessDateUtc = a.ProcessDateUtc,
                CompletedAtUtc = a.CompletedAtUtc,
                GatewayTransactionId = a.GatewayTransactionId,
                GatewayErrorCode = a.GatewayErrorCode,
                HasReceipt = a.ReceiptUrl != null,
                Anchored = a.ReceiptAnchorRef != null
            })
            .ToListAsync(ct);

        return Result<PagedResult<PayoutAuditRowDto>>.Success(new PagedResult<PayoutAuditRowDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }
}
