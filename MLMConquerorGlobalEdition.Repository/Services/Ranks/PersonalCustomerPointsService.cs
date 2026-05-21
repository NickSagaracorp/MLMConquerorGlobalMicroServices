using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <inheritdoc />
public sealed class PersonalCustomerPointsService : IPersonalCustomerPointsService
{
    private readonly AppDbContext _db;

    public PersonalCustomerPointsService(AppDbContext db) => _db = db;

    public async Task<int> GetMembershipPointsAsync(string memberId, CancellationToken ct = default)
        => await SumMembershipPointsAsync(new[] { memberId }, ct);

    public async Task<int> GetPersonalCustomerPointsAsync(string memberId, CancellationToken ct = default)
    {
        var ids = new List<string> { memberId };
        ids.AddRange(await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId)
            .Select(m => m.MemberId)
            .ToListAsync(ct));

        return await SumMembershipPointsAsync(ids, ct);
    }

    /// <summary>
    /// Sum of QualificationPoins across every product on the active-membership order
    /// of each supplied member. Non-Active memberships contribute nothing.
    /// </summary>
    private async Task<int> SumMembershipPointsAsync(IReadOnlyCollection<string> memberIds, CancellationToken ct)
    {
        var orderIds = await _db.MembershipSubscriptions.AsNoTracking()
            .Where(s => memberIds.Contains(s.MemberId)
                        && s.SubscriptionStatus == MembershipStatus.Active
                        && s.LastOrderId != null)
            .Select(s => s.LastOrderId!)
            .Distinct()
            .ToListAsync(ct);

        if (orderIds.Count == 0)
            return 0;

        return await (
            from od in _db.OrderDetails.AsNoTracking()
            join p in _db.Products.AsNoTracking() on od.ProductId equals p.Id
            where orderIds.Contains(od.OrderId)
            select p.QualificationPoins
        ).SumAsync(ct);
    }
}
