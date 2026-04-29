using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetCredentialsHistory;

public class GetCredentialsHistoryHandler : IRequestHandler<GetCredentialsHistoryQuery, Result<PagedResult<CredentialChangeDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetCredentialsHistoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CredentialChangeDto>>> Handle(
        GetCredentialsHistoryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.MemberCredentialChangeLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.CreationDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new CredentialChangeDto
            {
                Id        = l.Id,
                ChangedAt = l.CreationDate,
                ChangedBy = l.CreatedBy,
                Kind      = l.Kind.ToString(),
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent
            })
            .ToListAsync(ct);

        return Result<PagedResult<CredentialChangeDto>>.Success(new PagedResult<CredentialChangeDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }
}
