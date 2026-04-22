using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Membership details for a specific member — used by Admin Member Profile membership tab.
/// Route: /api/v1/admin/members/{memberId}/membership
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/membership")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberMembershipController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminMemberMembershipController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns the active membership subscription for a member plus all related orders.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMembership(string memberId, CancellationToken ct = default)
    {
        var subscription = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => s.MemberId == memberId)
            .OrderByDescending(s => s.CreationDate)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return Ok(ApiResponse<MemberMembershipDto?>.Ok(null));

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.OrderDetails)
            .Where(o => o.MembershipSubscriptionId == subscription.Id)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new MembershipOrderDto
            {
                OrderId       = o.Id,
                OrderNo       = o.OrderNo ?? o.Id,
                OrderDate     = o.OrderDate,
                TotalAmount   = o.TotalAmount,
                Status        = o.Status.ToString(),
                Notes         = o.Notes
            })
            .ToListAsync(ct);

        var dto = new MemberMembershipDto
        {
            SubscriptionId      = subscription.Id,
            MembershipLevelId   = subscription.MembershipLevelId,
            MembershipLevelName = subscription.MembershipLevel?.Name ?? string.Empty,
            Status              = subscription.SubscriptionStatus.ToString(),
            ChangeReason        = subscription.ChangeReason.ToString(),
            StartDate           = subscription.StartDate,
            EndDate             = subscription.EndDate,
            RenewalDate         = subscription.RenewalDate,
            HoldDate            = subscription.HoldDate,
            CancellationDate    = subscription.CancellationDate,
            IsFree              = subscription.IsFree,
            IsAutoRenew         = subscription.IsAutoRenew,
            Orders              = orders
        };

        return Ok(ApiResponse<MemberMembershipDto>.Ok(dto));
    }
}

public class MemberMembershipDto
{
    public string   SubscriptionId      { get; set; } = string.Empty;
    public int      MembershipLevelId   { get; set; }
    public string   MembershipLevelName { get; set; } = string.Empty;
    public string   Status              { get; set; } = string.Empty;
    public string   ChangeReason        { get; set; } = string.Empty;
    public DateTime StartDate           { get; set; }
    public DateTime? EndDate            { get; set; }
    public DateTime? RenewalDate        { get; set; }
    public DateTime? HoldDate           { get; set; }
    public DateTime? CancellationDate   { get; set; }
    public bool     IsFree              { get; set; }
    public bool     IsAutoRenew         { get; set; }
    public List<MembershipOrderDto> Orders { get; set; } = new();
}

public class MembershipOrderDto
{
    public string   OrderId     { get; set; } = string.Empty;
    public string   OrderNo     { get; set; } = string.Empty;
    public DateTime OrderDate   { get; set; }
    public decimal  TotalAmount { get; set; }
    public string   Status      { get; set; } = string.Empty;
    public string?  Notes       { get; set; }
}
