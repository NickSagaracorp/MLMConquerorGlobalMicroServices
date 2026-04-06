using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetSecurityLog;

public class GetSecurityLogHandler : IRequestHandler<GetSecurityLogQuery, Result<PagedResult<SecurityLogDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSecurityLogHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<SecurityLogDto>>> Handle(
        GetSecurityLogQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        // Surface audit trail entries (profile, credentials, wallet changes) for the member's account
        var query = _db.AuditTracking
            .AsNoTracking()
            .Where(a => a.ChangedBy == userId)
            .OrderByDescending(a => a.ChangedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new SecurityLogDto
            {
                EventType  = $"{a.Action} {a.EntityName}",
                IpAddress  = "—",
                OccurredAt = a.ChangedAt,
                Status     = "Success"
            })
            .ToListAsync(ct);

        return Result<PagedResult<SecurityLogDto>>.Success(new PagedResult<SecurityLogDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = request.Page,
            PageSize   = request.PageSize
        });
    }
}
