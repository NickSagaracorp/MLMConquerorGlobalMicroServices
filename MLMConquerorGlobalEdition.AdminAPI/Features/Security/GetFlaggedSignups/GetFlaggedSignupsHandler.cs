using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignups;

public class GetFlaggedSignupsHandler
    : IRequestHandler<GetFlaggedSignupsQuery, Result<PagedResult<FlaggedSignupDto>>>
{
    private readonly AppDbContext _db;

    public GetFlaggedSignupsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<FlaggedSignupDto>>> Handle(
        GetFlaggedSignupsQuery q, CancellationToken ct)
    {
        var page     = q.Page     <= 0 ? 1  : q.Page;
        var pageSize = q.PageSize <= 0 ? 25 : (q.PageSize > 200 ? 200 : q.PageSize);

        var query = _db.SignupRiskFingerprints.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.VisitorId))
            query = query.Where(x => x.VisitorId == q.VisitorId);

        if (!string.IsNullOrWhiteSpace(q.IpAddress))
            query = query.Where(x => x.IpAddress == q.IpAddress);

        if (!string.IsNullOrWhiteSpace(q.SponsorReplicateSite))
            query = query.Where(x => x.SponsorReplicateSite == q.SponsorReplicateSite);

        if (q.From.HasValue) query = query.Where(x => x.CreationDate >= q.From.Value);
        if (q.To.HasValue)   query = query.Where(x => x.CreationDate <= q.To.Value);

        if (q.OnlyFlagged)        query = query.Where(x => x.IsFlagged);
        if (!q.IncludeCleared)    query = query.Where(x => !x.Cleared);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FlaggedSignupDto
            {
                Id                   = x.Id,
                VisitorId            = x.VisitorId,
                Flow                 = x.Flow.ToString(),
                SponsorReplicateSite = x.SponsorReplicateSite,
                IpAddress            = x.IpAddress,
                UserAgent            = x.UserAgent,
                OrderId              = x.OrderId,
                MemberId             = x.MemberId,
                IsFlagged            = x.IsFlagged,
                FlagReason           = x.FlagReason,
                Cleared              = x.Cleared,
                ClearedAt            = x.ClearedAt,
                ClearedBy            = x.ClearedBy,
                ClearReason          = x.ClearReason,
                CreationDate         = x.CreationDate
            })
            .ToListAsync(ct);

        return Result<PagedResult<FlaggedSignupDto>>.Success(new PagedResult<FlaggedSignupDto>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }
}
