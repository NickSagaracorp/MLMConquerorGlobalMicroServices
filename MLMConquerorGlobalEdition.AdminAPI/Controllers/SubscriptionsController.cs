using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Subscriptions;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubscriptionsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a paged list of membership subscriptions, optionally filtered by status.
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Items per page (default: 25).</param>
    /// <param name="status">Optional filter — "Active", "Expired", or "Cancelled".</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 25;

        var query = _db.MembershipSubscriptions
            .AsNoTracking()
            .Include(s => s.MembershipLevel)
            .AsQueryable();

        // Map the incoming string to the MembershipStatus enum value
        if (!string.IsNullOrWhiteSpace(status))
        {
            var mapped = status.ToLowerInvariant() switch
            {
                "active"    => MembershipStatus.Active,
                "expired"   => MembershipStatus.Expired,
                "cancelled" => MembershipStatus.Cancelled,
                _           => (MembershipStatus?)null
            };

            if (mapped is null)
                return BadRequest(ApiResponse<PagedResult<SubscriptionDto>>.Fail(
                    "INVALID_STATUS",
                    $"Status '{status}' is not valid. Accepted values: Active, Expired, Cancelled."));

            query = query.Where(s => s.SubscriptionStatus == mapped.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SubscriptionDto
            {
                Id                  = s.Id,
                MemberId            = s.MemberId,
                MembershipLevelId   = s.MembershipLevelId,
                MembershipLevelName = s.MembershipLevel != null ? s.MembershipLevel.Name : string.Empty,
                Status              = s.SubscriptionStatus.ToString(),
                ChangeReason        = s.ChangeReason.ToString(),
                StartDate           = s.StartDate,
                ExpirationDate      = s.EndDate,
                IsAutoRenew         = s.IsAutoRenew,
                IsFree              = s.IsFree,
                CreationDate        = s.CreationDate
            })
            .ToListAsync(ct);

        var result = new PagedResult<SubscriptionDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };

        return Ok(ApiResponse<PagedResult<SubscriptionDto>>.Ok(result));
    }
}
