using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewayLog;

public class GetPayoutGatewayLogHandler : IRequestHandler<GetPayoutGatewayLogQuery, Result<List<PayoutGatewayLogDto>>>
{
    private readonly AppDbContext _db;

    public GetPayoutGatewayLogHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<PayoutGatewayLogDto>>> Handle(GetPayoutGatewayLogQuery r, CancellationToken ct)
    {
        // The raw gateway log keys on member, not attempt id.
        // Scoped to the attempt's member — the detail screen provides the member context.
        var logs = await _db.WalletApiLogs.AsNoTracking()
            .Where(l => l.MemberId == r.MemberId)
            .OrderByDescending(l => l.CreationDate)
            .Select(l => new PayoutGatewayLogDto
            {
                Id = l.Id,
                WalletType = l.WalletType,
                Operation = l.Operation,
                HttpStatusCode = l.HttpStatusCode,
                Success = l.Success,
                ErrorMessage = l.ErrorMessage,
                DurationMs = l.DurationMs,
                CreationDate = l.CreationDate
            })
            .ToListAsync(ct);

        return Result<List<PayoutGatewayLogDto>>.Success(logs);
    }
}
