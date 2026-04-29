using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Development-only endpoint to seed the initial SuperAdmin user.
/// Disabled automatically in Production via the env check.
/// </summary>
[ApiController]
[Route("api/v1/dev")]
public class DevSeedController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole>    _roleManager;
    private readonly IWebHostEnvironment          _env;
    private readonly IDateTimeProvider            _dateTime;
    private readonly AppDbContext                 _db;

    public DevSeedController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole>    roleManager,
        IWebHostEnvironment          env,
        IDateTimeProvider            dateTime,
        AppDbContext                 db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _env         = env;
        _dateTime    = dateTime;
        _db          = db;
    }

    /// <summary>POST /api/v1/dev/seed-superadmin — creates roles + SuperAdmin user.</summary>
    [HttpPost("seed-superadmin")]
    public async Task<ActionResult<ApiResponse<SeedResultDto>>> SeedSuperAdmin(
        [FromBody] SeedRequest request,
        CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var roles = new[]
        {
            "SuperAdmin", "Admin", "CommissionManager", "BillingManager",
            "SupportManager", "SupportLevel1", "SupportLevel2", "SupportLevel3",
            "IT", "Ambassador", "Member"
        };

        foreach (var role in roles)
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Ok(ApiResponse<SeedResultDto>.Ok(new SeedResultDto("User already exists — skipped.")));

        var user = new ApplicationUser
        {
            UserName      = request.Email,
            Email         = request.Email,
            EmailConfirmed = true,
            IsActive      = true,
            CreationDate  = _dateTime.Now,
            CreatedBy     = "DevSeed"
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(ApiResponse<SeedResultDto>.Fail("SEED_FAILED", errors));
        }

        await _userManager.AddToRoleAsync(user, "SuperAdmin");

        return Ok(ApiResponse<SeedResultDto>.Ok(
            new SeedResultDto($"SuperAdmin '{request.Email}' created successfully.")));
    }

    /// <summary>
    /// POST /api/v1/dev/fix-membership-status
    /// One-time fix: activates all subscriptions whose member profile is Active but subscription is still Pending,
    /// and sets EndDate + RenewalDate = StartDate.AddMonths(1) for any subscription missing those dates.
    /// </summary>
    [HttpPost("fix-membership-status")]
    public async Task<ActionResult<ApiResponse<FixMembershipResultDto>>> FixMembershipStatus(
        CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var now = _dateTime.Now;

        // Load active member IDs
        var activeMemberIds = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.Status == MemberAccountStatus.Active)
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        // Load all subscriptions for those members
        var subscriptions = await _db.MembershipSubscriptions
            .Where(s => activeMemberIds.Contains(s.MemberId))
            .ToListAsync(ct);

        // Deduplicate: one most-recent subscription per member
        var latestPerMember = subscriptions
            .GroupBy(s => s.MemberId)
            .Select(g => g.OrderByDescending(s => s.CreationDate).First())
            .ToList();

        int activatedCount = 0;
        int datesFixedCount = 0;

        foreach (var sub in latestPerMember)
        {
            if (sub.SubscriptionStatus == MembershipStatus.Pending)
            {
                sub.SubscriptionStatus = MembershipStatus.Active;
                sub.LastUpdateDate     = now;
                sub.LastUpdateBy       = "fix-membership-status";
                activatedCount++;
            }

            if (sub.EndDate is null || sub.RenewalDate is null)
            {
                var startDate      = sub.StartDate == default ? now : sub.StartDate;
                sub.StartDate      = startDate;
                sub.EndDate        = startDate.AddMonths(1);
                sub.RenewalDate    = startDate.AddMonths(1);
                sub.LastUpdateDate = now;
                sub.LastUpdateBy   = "fix-membership-status";
                datesFixedCount++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<FixMembershipResultDto>.Ok(
            new FixMembershipResultDto(activatedCount, datesFixedCount,
                $"Done. {activatedCount} subscriptions activated, {datesFixedCount} subscriptions had dates set.")));
    }

    /// <summary>GET /api/v1/dev/subscription-stats — raw subscription counts + paged query test.</summary>
    [HttpGet("subscription-stats")]
    public async Task<IActionResult> SubscriptionStats(CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var all = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Select(s => new { s.MemberId, Status = s.SubscriptionStatus.ToString(), s.StartDate, s.EndDate, s.RenewalDate })
            .ToListAsync(ct);

        var byStatus = all.GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToList();

        // Simulate the exact SubscriptionsController query
        string? queryError = null;
        int pagedCount = 0;
        try
        {
            var items = await _db.MembershipSubscriptions
                .AsNoTracking()
                .Include(s => s.MembershipLevel)
                .OrderByDescending(s => s.CreationDate)
                .Take(25)
                .Select(s => new {
                    s.Id, s.MemberId, s.MembershipLevelId,
                    MembershipLevelName = s.MembershipLevel != null ? s.MembershipLevel.Name : "",
                    Status = s.SubscriptionStatus.ToString(),
                    ChangeReason = s.ChangeReason.ToString(),
                    s.StartDate, ExpirationDate = s.EndDate, s.IsAutoRenew, s.IsFree, s.CreationDate
                })
                .ToListAsync(ct);
            pagedCount = items.Count;
        }
        catch (Exception ex)
        {
            queryError = ex.Message;
        }

        return Ok(new { total = all.Count, byStatus, pagedCount, queryError, sample = all.Take(3) });
    }

    /// <summary>
    /// POST /api/v1/dev/fix-member-orders
    /// One-time fix: marks the most recent Pending order as Completed for every Active member.
    /// Required when members were activated manually (bypassing CompleteSignup flow).
    /// </summary>
    [HttpPost("fix-member-orders")]
    public async Task<ActionResult<ApiResponse<FixMemberOrdersResultDto>>> FixMemberOrders(
        CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var now = _dateTime.Now;

        var activeMemberIds = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.Status == MemberAccountStatus.Active)
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        var pendingOrders = await _db.Orders
            .Where(o => activeMemberIds.Contains(o.MemberId) && o.Status == Domain.Entities.Orders.OrderStatus.Pending)
            .ToListAsync(ct);

        // Keep only the most recent pending order per member
        var toFix = pendingOrders
            .GroupBy(o => o.MemberId)
            .Select(g => g.OrderByDescending(o => o.CreationDate).First())
            .ToList();

        foreach (var order in toFix)
        {
            order.Status         = Domain.Entities.Orders.OrderStatus.Completed;
            order.OrderDate      = order.OrderDate == default ? now : order.OrderDate;
            order.LastUpdateDate = now;
            order.LastUpdateBy   = "fix-member-orders";
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<FixMemberOrdersResultDto>.Ok(
            new FixMemberOrdersResultDto(toFix.Count,
                $"Done. {toFix.Count} pending orders marked as Completed.")));
    }

    /// <summary>
    /// POST /api/v1/dev/force-place-all
    /// Dev-only: immediately places ALL active ambassadors who are not yet in the binary tree,
    /// ignoring the 30-day placement window. Useful after seeding test members.
    /// </summary>
    [HttpPost("force-place-all")]
    public async Task<IActionResult> ForcePlaceAll(CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var now = _dateTime.Now;

        var allMembers = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.SponsorMemberId != null && m.Status == MemberAccountStatus.Active)
            .Select(m => new { m.MemberId, m.SponsorMemberId })
            .ToListAsync(ct);

        var alreadyPlaced = await _db.DualTeamTree
            .AsNoTracking()
            .Select(d => d.MemberId)
            .ToHashSetAsync(ct);

        var toPlace = allMembers
            .Where(m => !alreadyPlaced.Contains(m.MemberId))
            .ToList();

        if (toPlace.Count == 0)
            return Ok(ApiResponse<ForcePlaceResultDto>.Ok(new ForcePlaceResultDto(0, "All members are already placed.")));

        int placed = 0;
        int failed = 0;
        var errors = new List<string>();

        foreach (var m in toPlace)
        {
            try
            {
                await PlaceMemberAsync(m.MemberId, m.SponsorMemberId!, now);
                await _db.SaveChangesAsync(ct);
                placed++;
                // Refresh placed set so subsequent members see newly-placed members
                alreadyPlaced.Add(m.MemberId);
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{m.MemberId}: {ex.Message}");
            }
        }

        return Ok(ApiResponse<ForcePlaceResultDto>.Ok(
            new ForcePlaceResultDto(placed, $"Placed {placed}, failed {failed}. Errors: {string.Join("; ", errors)}")));
    }

    private async Task PlaceMemberAsync(string memberId, string sponsorMemberId, DateTime now)
    {
        var sponsorNode = await _db.DualTeamTree
            .FirstOrDefaultAsync(d => d.MemberId == sponsorMemberId);

        string   targetParentId;
        TreeSide targetSide;

        if (sponsorNode is null)
        {
            targetParentId = sponsorMemberId;
            targetSide     = TreeSide.Left;

            if (!await _db.DualTeamTree.AnyAsync(d => d.MemberId == sponsorMemberId))
            {
                _db.DualTeamTree.Add(new DualTeamEntity
                {
                    MemberId       = sponsorMemberId,
                    ParentMemberId = null,
                    Side           = TreeSide.Left,
                    HierarchyPath  = $"/{sponsorMemberId}/",
                    CreationDate   = now,
                    CreatedBy      = "force-place",
                    LastUpdateDate = now,
                    LastUpdateBy   = "force-place"
                });
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            var children = await _db.DualTeamTree
                .AsNoTracking()
                .Where(d => d.ParentMemberId == sponsorMemberId)
                .ToListAsync();

            var hasLeft  = children.Any(c => c.Side == TreeSide.Left);
            var hasRight = children.Any(c => c.Side == TreeSide.Right);

            if (!hasLeft)
            {
                targetParentId = sponsorMemberId;
                targetSide     = TreeSide.Left;
            }
            else if (!hasRight)
            {
                targetParentId = sponsorMemberId;
                targetSide     = TreeSide.Right;
            }
            else
            {
                // Spill to deepest available slot on sponsor's own side
                var preferredSide = sponsorNode.ParentMemberId is null ? TreeSide.Left : sponsorNode.Side;
                (targetParentId, targetSide) = await FindDeepestSlotAsync(sponsorMemberId, preferredSide);
            }
        }

        var parentNode = await _db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == targetParentId);
        var parentPath = parentNode?.HierarchyPath ?? $"/{targetParentId}/";

        _db.DualTeamTree.Add(new DualTeamEntity
        {
            MemberId       = memberId,
            ParentMemberId = targetParentId,
            Side           = targetSide,
            HierarchyPath  = $"{parentPath}{memberId}/",
            CreationDate   = now,
            CreatedBy      = "force-place",
            LastUpdateDate = now,
            LastUpdateBy   = "force-place"
        });

        _db.PlacementLogs.Add(new PlacementLog
        {
            MemberId            = memberId,
            PlacedUnderMemberId = targetParentId,
            Side                = targetSide,
            Action              = "ForcePlaced",
            Reason              = "Dev force-place endpoint",
            UnplacementCount    = 0,
            FirstPlacementDate  = now,
            CreationDate        = now,
            CreatedBy           = "force-place"
        });
    }

    private async Task<(string NodeId, TreeSide Side)> FindDeepestSlotAsync(
        string sponsorMemberId, TreeSide preferredSide)
    {
        var startChild = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ParentMemberId == sponsorMemberId && d.Side == preferredSide);

        if (startChild is null) return (sponsorMemberId, preferredSide);

        var queue   = new Queue<string>();
        var visited = new HashSet<string>();
        queue.Enqueue(startChild.MemberId);

        string   bestNodeId = startChild.MemberId;
        TreeSide bestSide   = TreeSide.Left;
        int      bestDepth  = 0;

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!visited.Add(currentId)) continue;

            var children = await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId == currentId).ToListAsync();

            var hasLeft  = children.Any(c => c.Side == TreeSide.Left);
            var hasRight = children.Any(c => c.Side == TreeSide.Right);

            if (!hasLeft || !hasRight)
            {
                var node  = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == currentId);
                var depth = node?.HierarchyPath.Count(c => c == '/') ?? 0;
                if (depth >= bestDepth)
                {
                    bestNodeId = currentId;
                    bestSide   = !hasLeft ? TreeSide.Left : TreeSide.Right;
                    bestDepth  = depth;
                }
            }

            foreach (var child in children) queue.Enqueue(child.MemberId);
        }

        return (bestNodeId, bestSide);
    }

    /// <summary>
    /// POST /api/v1/dev/activate-user
    /// Dev-only: sets IsActive=true and EmailConfirmed=true on an ApplicationUser by email.
    /// Use this when a member completed signup but the CompleteSignup step failed.
    /// </summary>
    [HttpPost("activate-user")]
    public async Task<ActionResult<ApiResponse<SeedResultDto>>> ActivateUser(
        [FromBody] ActivateUserRequest request,
        CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return NotFound(ApiResponse<SeedResultDto>.Fail("NOT_FOUND", $"No user found with email '{request.Email}'."));

        user.IsActive        = true;
        user.EmailConfirmed  = true;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(ApiResponse<SeedResultDto>.Fail("UPDATE_FAILED", errors));
        }

        // Also activate the MemberProfile if it exists
        var member = !string.IsNullOrEmpty(user.MemberProfileId)
            ? await _db.MemberProfiles.FirstOrDefaultAsync(m => m.MemberId == user.MemberProfileId, ct)
            : await _db.MemberProfiles.FirstOrDefaultAsync(m => m.Email == request.Email, ct);

        if (member is not null && member.Status != MemberAccountStatus.Active)
        {
            member.Status         = MemberAccountStatus.Active;
            member.LastUpdateDate = _dateTime.Now;
            member.LastUpdateBy   = "dev-activate-user";

            // Also activate their latest pending subscription
            var sub = await _db.MembershipSubscriptions
                .Where(s => s.MemberId == member.MemberId)
                .OrderByDescending(s => s.CreationDate)
                .FirstOrDefaultAsync(ct);

            if (sub is not null && sub.SubscriptionStatus == MembershipStatus.Pending)
            {
                sub.SubscriptionStatus = MembershipStatus.Active;
                sub.StartDate          = sub.StartDate == default ? _dateTime.Now : sub.StartDate;
                sub.EndDate            = sub.StartDate.AddMonths(1);
                sub.RenewalDate        = sub.StartDate.AddMonths(1);
                sub.LastUpdateDate     = _dateTime.Now;
                sub.LastUpdateBy       = "dev-activate-user";
            }

            await _db.SaveChangesAsync(ct);
        }

        return Ok(ApiResponse<SeedResultDto>.Ok(
            new SeedResultDto($"User '{request.Email}' activated successfully (IsActive=true, EmailConfirmed=true).")));
    }

    public record SeedRequest(string Email, string Password);
    public record ActivateUserRequest(string Email);
    public record SeedResultDto(string Message);
    public record FixMembershipResultDto(int Activated, int DatesFixed, string Message);
    public record FixMemberOrdersResultDto(int Fixed, string Message);
    public record ForcePlaceResultDto(int Placed, string Message);
}
