using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.Domain.Entities.General;
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

        var query = ApplyPageFilters(
            _db.SignupRiskFingerprints.AsNoTracking(),
            q.VisitorId, q.IpAddress, q.SponsorReplicateSite, q.From, q.To, q.OnlyFlagged, q.IncludeCleared);

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

    /// <summary>
    /// Filtros de la página (visitante, IP, sitio del patrocinador, rango de fechas, solo marcados, incluir
    /// liberados), todos opcionales y unidos con AND. Los comparten este listado y el grid de servidor
    /// (GetFlaggedSignupsGridHandler) para que los dos respondan lo mismo a los mismos filtros.
    /// </summary>
    internal static IQueryable<SignupRiskFingerprint> ApplyPageFilters(
        IQueryable<SignupRiskFingerprint> query,
        string? visitorId, string? ipAddress, string? sponsorReplicateSite,
        DateTime? from, DateTime? to, bool onlyFlagged, bool includeCleared)
    {
        if (!string.IsNullOrWhiteSpace(visitorId))
            query = query.Where(x => x.VisitorId == visitorId);

        if (!string.IsNullOrWhiteSpace(ipAddress))
            query = query.Where(x => x.IpAddress == ipAddress);

        if (!string.IsNullOrWhiteSpace(sponsorReplicateSite))
            query = query.Where(x => x.SponsorReplicateSite == sponsorReplicateSite);

        if (from.HasValue) query = query.Where(x => x.CreationDate >= from.Value);
        if (to.HasValue)   query = query.Where(x => x.CreationDate <= to.Value);

        if (onlyFlagged)     query = query.Where(x => x.IsFlagged);
        if (!includeCleared) query = query.Where(x => !x.Cleared);

        return query;
    }
}
