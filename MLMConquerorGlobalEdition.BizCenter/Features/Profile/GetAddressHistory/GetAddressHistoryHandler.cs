using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetAddressHistory;

public class GetAddressHistoryHandler : IRequestHandler<GetAddressHistoryQuery, Result<PagedResult<AddressHistoryDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetAddressHistoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<AddressHistoryDto>>> Handle(GetAddressHistoryQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.MemberAddressHistories
            .AsNoTracking()
            .Where(h => h.MemberId == memberId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(h => h.CreationDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(h => new AddressHistoryDto
            {
                Id              = h.Id,
                ChangedAt       = h.CreationDate,
                ChangedBy       = h.CreatedBy,
                PreviousAddress = h.PreviousAddress,
                PreviousCity    = h.PreviousCity,
                PreviousState   = h.PreviousState,
                PreviousZipCode = h.PreviousZipCode,
                PreviousCountry = h.PreviousCountry,
                NewAddress      = h.NewAddress,
                NewCity         = h.NewCity,
                NewState        = h.NewState,
                NewZipCode      = h.NewZipCode,
                NewCountry      = h.NewCountry,
                Reason          = h.Reason
            })
            .ToListAsync(ct);

        return Result<PagedResult<AddressHistoryDto>>.Success(new PagedResult<AddressHistoryDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }
}
