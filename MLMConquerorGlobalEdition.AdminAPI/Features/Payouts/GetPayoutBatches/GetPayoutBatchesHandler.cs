using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatches;

public class GetPayoutBatchesHandler
    : IRequestHandler<GetPayoutBatchesQuery, Result<PagedResult<PayoutBatchRowDto>>>
{
    private readonly AppDbContext _db;

    public GetPayoutBatchesHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<PayoutBatchRowDto>>> Handle(
        GetPayoutBatchesQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var query = _db.PayoutBatches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(b => b.Status == request.Status);

        if (request.WalletType.HasValue)
            query = query.Where(b => b.WalletType == request.WalletType.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(b => b.CreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new PayoutBatchRowDto
            {
                Id = b.Id,
                WalletType = b.WalletType,
                ProcessDateUtc = b.ProcessDateUtc,
                Status = b.Status,
                MemberCount = b.MemberCount,
                TotalAmountUsd = b.TotalAmountUsd,
                CreationDate = b.CreationDate,
                ReconciledAt = b.ReconciledAt
            })
            .ToListAsync(ct);

        return Result<PagedResult<PayoutBatchRowDto>>.Success(new PagedResult<PayoutBatchRowDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }
}
