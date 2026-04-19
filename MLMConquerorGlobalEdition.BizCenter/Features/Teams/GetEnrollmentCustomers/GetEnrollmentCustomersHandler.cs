using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentCustomers;

public class GetEnrollmentCustomersHandler
    : IRequestHandler<GetEnrollmentCustomersQuery, Result<PagedResult<EnrollmentCustomerDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetEnrollmentCustomersHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<EnrollmentCustomerDto>>> Handle(
        GetEnrollmentCustomersQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var myNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);

        if (myNode is null)
            return Result<PagedResult<EnrollmentCustomerDto>>.Success(
                new PagedResult<EnrollmentCustomerDto>());

        var pathPrefix = myNode.HierarchyPath;

        var downlineNodes = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix))
            .Select(g => new { g.MemberId, g.Level })
            .ToListAsync(ct);

        var downlineIds = downlineNodes.Select(x => x.MemberId).ToList();
        var levelMap    = downlineNodes.ToDictionary(x => x.MemberId, x => x.Level);

        if (!downlineIds.Any())
            return Result<PagedResult<EnrollmentCustomerDto>>.Success(
                new PagedResult<EnrollmentCustomerDto>());

        // Only ExternalMember type
        var query = _db.MemberProfiles
            .AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId) &&
                        m.MemberType == MemberType.ExternalMember);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            query = query.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(ct);

        var profiles = await query
            .OrderByDescending(m => m.EnrollDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new
            {
                m.MemberId, m.FirstName, m.LastName, m.Email, m.Phone,
                m.Country, m.EnrollDate, m.SponsorMemberId,
                AccountStatus = m.Status.ToString()
            })
            .ToListAsync(ct);

        var pageIds = profiles.Select(p => p.MemberId).ToList();

        var subscriptions = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => pageIds.Contains(s.MemberId))
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        var subMap = subscriptions
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.First());

        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var sponsorIds = profiles
            .Where(p => !string.IsNullOrEmpty(p.SponsorMemberId))
            .Select(p => p.SponsorMemberId!)
            .Distinct().ToList();

        var nameMap = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => sponsorIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        var items = profiles.Select(p =>
        {
            subMap.TryGetValue(p.MemberId, out var sub);
            stats.TryGetValue(p.MemberId, out var stat);
            nameMap.TryGetValue(p.SponsorMemberId ?? "", out var sponsorName);

            return new EnrollmentCustomerDto
            {
                MemberId         = p.MemberId,
                FullName         = $"{p.FirstName} {p.LastName}",
                Email            = p.Email,
                Phone            = p.Phone,
                Country          = p.Country,
                Level            = levelMap.TryGetValue(p.MemberId, out var lvl) ? lvl : 0,
                EnrollDate       = p.EnrollDate,
                SponsorMemberId  = p.SponsorMemberId,
                SponsorFullName  = sponsorName,
                AccountStatus    = p.AccountStatus,
                MembershipStatus = sub?.SubscriptionStatus.ToString() ?? "None",
                MembershipLevel  = sub?.MembershipLevel?.Name,
                PersonalPoints   = stat?.PersonalPoints ?? 0
            };
        }).ToList();

        return Result<PagedResult<EnrollmentCustomerDto>>.Success(
            new PagedResult<EnrollmentCustomerDto>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = request.Page,
                PageSize   = request.PageSize
            });
    }
}
