using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
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
    private readonly IPlacementService            _placement;

    public DevSeedController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole>    roleManager,
        IWebHostEnvironment          env,
        IDateTimeProvider            dateTime,
        AppDbContext                 db,
        IPlacementService            placement)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _env         = env;
        _dateTime    = dateTime;
        _db          = db;
        _placement   = placement;
    }

    /// <summary>
    /// POST /api/v1/dev/place-bulk — scalable backfill that places every unplaced member
    /// (optionally only those sponsored by <c>sponsorIds</c>) through the single
    /// <see cref="IPlacementService"/> authority. Replaces the raw-SQL placement shortcut:
    /// deepest-chain spillover, idempotent, O(1) slot finding, one deferred leg-point pass.
    /// Dev-only.
    /// </summary>
    [HttpPost("place-bulk")]
    public async Task<ActionResult<ApiResponse<BulkPlacementOutcome>>> PlaceBulk(
        [FromBody] PlaceBulkRequest? request, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var query = _db.MemberProfiles.AsNoTracking()
            .Where(m => !m.IsDeleted && m.SponsorMemberId != null)
            .Where(m => !_db.DualTeamTree.Any(d => d.MemberId == m.MemberId));

        if (request?.SponsorIds is { Count: > 0 } sponsors)
            query = query.Where(m => sponsors.Contains(m.SponsorMemberId!));

        var pairs = await query
            .Select(m => new { m.MemberId, m.SponsorMemberId })
            .ToListAsync(ct);

        var result = await _placement.PlaceBulkAsync(
            pairs.Select(p => (p.MemberId, p.SponsorMemberId!)).ToList(), ct);

        return result.IsSuccess
            ? Ok(ApiResponse<BulkPlacementOutcome>.Ok(result.Value!))
            : BadRequest(ApiResponse<BulkPlacementOutcome>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>
    /// POST /api/v1/dev/backfill-frontier — one-time: rebuild the DualTeamLegFrontier cache
    /// from the live tree. For every sponsor whose two slots are full, record the deepest
    /// node of its preferred-side subtree. Idempotent. Dev-only.
    /// </summary>
    [HttpPost("backfill-frontier")]
    public async Task<ActionResult<ApiResponse<int>>> BackfillFrontier(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        // Rebuild the frontier cache from the live tree. We compute it in memory rather than
        // via a path-prefix SQL join: HierarchyPath is now nvarchar(max) and unindexed (see
        // migration MakeHierarchyPathUnboundedDropPathIndex), so a per-leg-root
        // `LIKE legRootPath + '%'` correlated subquery is O(fullSponsors × tree) full scans
        // and times out on an 80k-node tree. Adjacency fits in memory; one descending-depth
        // pass propagates each node's deepest descendant up to all its ancestors in O(N).
        var nodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Select(d => new { d.MemberId, d.ParentMemberId, d.Side, d.HierarchyPath })
            .ToListAsync(ct);

        // depth = slash count, matching DualTeamPlacementRules.Depth so DeepestDepth lines up
        // with what PlaceAsync compares against.
        static int Depth(string p) => p.Count(c => c == '/');

        var parentOf = new Dictionary<string, string?>(nodes.Count);
        var sideOf = new Dictionary<string, TreeSide>(nodes.Count);
        var childSides = new Dictionary<string, (bool Left, bool Right, string? LeftChild, string? RightChild)>();
        // deepest[node] = the deepest descendant within node's subtree (including itself).
        var deepest = new Dictionary<string, (string Id, int Depth)>(nodes.Count);

        foreach (var n in nodes)
        {
            parentOf[n.MemberId] = n.ParentMemberId;
            sideOf[n.MemberId] = n.Side;
            deepest[n.MemberId] = (n.MemberId, Depth(n.HierarchyPath));
            if (n.ParentMemberId is not null)
            {
                var slot = childSides.GetValueOrDefault(n.ParentMemberId);
                if (n.Side == TreeSide.Left) slot = (true, slot.Right, n.MemberId, slot.RightChild);
                else slot = (slot.Left, true, slot.LeftChild, n.MemberId);
                childSides[n.ParentMemberId] = slot;
            }
        }

        // Propagate deepest-descendant up the tree. Processing in descending depth guarantees
        // every node is finalized before its (shallower) parent, so one pass suffices.
        foreach (var n in nodes.OrderByDescending(x => Depth(x.HierarchyPath)))
        {
            if (n.ParentMemberId is null) continue;
            var cur = deepest[n.MemberId];
            var par = deepest[n.ParentMemberId];
            // Tie-break on smaller MemberId to match the previous SQL (ORDER BY depth DESC, MemberId).
            if (cur.Depth > par.Depth ||
                (cur.Depth == par.Depth && string.CompareOrdinal(cur.Id, par.Id) < 0))
                deepest[n.ParentMemberId] = cur;
        }

        var existing = await _db.DualTeamLegFrontiers.ToDictionaryAsync(f => f.SponsorMemberId, ct);
        int affected = 0;

        foreach (var (sponsorId, slot) in childSides)
        {
            if (!slot.Left || !slot.Right) continue; // only full sponsors spill

            var preferredSide = parentOf[sponsorId] is null ? TreeSide.Left : sideOf[sponsorId];
            var legRoot = preferredSide == TreeSide.Left ? slot.LeftChild : slot.RightChild;
            if (legRoot is null) continue;

            var (deepId, deepDepth) = deepest[legRoot];

            if (existing.TryGetValue(sponsorId, out var f))
            {
                f.PreferredSide = preferredSide;
                f.DeepestMemberId = deepId;
                f.DeepestDepth = deepDepth;
            }
            else
            {
                _db.DualTeamLegFrontiers.Add(new DualTeamLegFrontier
                {
                    SponsorMemberId = sponsorId,
                    PreferredSide = preferredSide,
                    DeepestMemberId = deepId,
                    DeepestDepth = deepDepth,
                    CreatedBy = "backfill"
                });
            }
            affected++;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<int>.Ok(affected));
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
        {
            // Dev convenience: re-assign the SuperAdmin role and reset the password so the
            // account is always usable for verification runs, even if the original password is unknown.
            if (!await _userManager.IsInRoleAsync(existing, "SuperAdmin"))
                await _userManager.AddToRoleAsync(existing, "SuperAdmin");

            existing.IsActive       = true;
            existing.EmailConfirmed = true;
            await _userManager.UpdateAsync(existing);

            var token       = await _userManager.GeneratePasswordResetTokenAsync(existing);
            var resetResult = await _userManager.ResetPasswordAsync(existing, token, request.Password);
            if (!resetResult.Succeeded)
            {
                var resetErrors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                return BadRequest(ApiResponse<SeedResultDto>.Fail("SEED_FAILED", resetErrors));
            }

            return Ok(ApiResponse<SeedResultDto>.Ok(
                new SeedResultDto($"User '{request.Email}' already existed — password reset and SuperAdmin role ensured.")));
        }

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

        // Superseded by place-bulk: this now delegates to the single IPlacementService
        // authority instead of the old bespoke raw-insert logic (which raced the
        // AutoPlacementJob and produced the duplicate / side-collided rows the
        // placement-engine redesign fixed). Kept as an alias so existing dev scripts
        // that POST force-place-all still work, but they get the correct engine.
        var pairs = await _db.MemberProfiles.AsNoTracking()
            .Where(m => !m.IsDeleted && m.SponsorMemberId != null && m.Status == MemberAccountStatus.Active)
            .Where(m => !_db.DualTeamTree.Any(d => d.MemberId == m.MemberId))
            .Select(m => new { m.MemberId, m.SponsorMemberId })
            .ToListAsync(ct);

        if (pairs.Count == 0)
            return Ok(ApiResponse<ForcePlaceResultDto>.Ok(new ForcePlaceResultDto(0, "All members are already placed.")));

        var result = await _placement.PlaceBulkAsync(
            pairs.Select(p => (p.MemberId, p.SponsorMemberId!)).ToList(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ForcePlaceResultDto>.Fail(result.ErrorCode!, result.Error!));

        var o = result.Value!;
        return Ok(ApiResponse<ForcePlaceResultDto>.Ok(
            new ForcePlaceResultDto(o.Placed, $"Placed {o.Placed}, skipped {o.Skipped}, failed {o.Failed} (via placement engine).")));
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

    /// <summary>
    /// POST /api/v1/dev/respace-rank-achievements
    /// One-time data correction for the multi-rank-climb timestamp collision: before the
    /// EvaluateRankHandler monotonic-offset fix, every rank awarded in a single evaluation
    /// was stamped with the SAME AchievedAt (down to the millisecond), producing physically
    /// impossible history ("achieved two ranks at the exact same second"). This re-spaces each
    /// colliding group (same MemberId + same AchievedAt) by ordering on rank SortOrder and
    /// bumping each successive row by +N seconds — the identical logic the engine now applies
    /// going forward. Idempotent: rows already distinct are left untouched. Dev-only.
    /// </summary>
    [HttpPost("respace-rank-achievements")]
    public async Task<ActionResult<ApiResponse<RespaceRankResultDto>>> RespaceRankAchievements(
        CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var sortOrderByRank = await _db.RankDefinitions.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.SortOrder, ct);

        // Tracked load so mutations are persisted on SaveChanges.
        var rows = await _db.MemberRankHistories
            .Where(h => !h.IsDeleted)
            .ToListAsync(ct);

        int groupsFixed = 0;
        int rowsAdjusted = 0;

        foreach (var memberGroup in rows.GroupBy(h => h.MemberId))
        {
            foreach (var clash in memberGroup
                         .GroupBy(h => h.AchievedAt)
                         .Where(g => g.Count() > 1))
            {
                // Order by rank SortOrder so the earliest-rank keeps the original instant and
                // each higher rank lands one second later, matching the climb sequence.
                var ordered = clash
                    .OrderBy(h => sortOrderByRank.TryGetValue(h.RankDefinitionId, out var so) ? so : 0)
                    .ThenBy(h => h.Id)
                    .ToList();

                for (int i = 1; i < ordered.Count; i++)
                {
                    var shifted = ordered[i].AchievedAt.AddSeconds(i);
                    ordered[i].AchievedAt   = shifted;
                    ordered[i].CreationDate = shifted; // keep CreationDate aligned with the engine fix
                    rowsAdjusted++;
                }
                groupsFixed++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<RespaceRankResultDto>.Ok(new RespaceRankResultDto(
            groupsFixed, rowsAdjusted,
            $"Re-spaced {rowsAdjusted} colliding rank-history rows across {groupsFixed} groups.")));
    }

    /// <summary>
    /// POST /api/v1/dev/seed-payout-wallets — DEV ONLY. Gives the top-N members (by pending
    /// commission total) who lack an approved+preferred payout wallet a wallet on an active
    /// gateway, round-robin across the active PaymentGateways, so the Payouts dashboard has
    /// eligible candidates to display. Idempotent: members who already have an approved
    /// preferred wallet are skipped. Real payout data should come from signup, not this — it
    /// only exists so the payout screens can be exercised against the bulk-loaded test members.
    /// </summary>
    [HttpPost("seed-payout-wallets")]
    public async Task<ActionResult<ApiResponse<SeedPayoutWalletsResultDto>>> SeedPayoutWallets(
        [FromBody] SeedPayoutWalletsRequest? request, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var count = request?.Count is > 0 ? request.Count.Value : 500;
        var minTotal = request?.MinTotal ?? 25m;

        var activeTypes = await _db.PaymentGateways.AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.WalletType)
            .Select(g => g.WalletType)
            .ToListAsync(ct);

        if (activeTypes.Count == 0)
            return BadRequest(ApiResponse<SeedPayoutWalletsResultDto>.Fail(
                "NO_ACTIVE_GATEWAYS", "No active payout gateways are configured."));

        // Members who already have an approved preferred wallet are skipped.
        var membersWithWallet = _db.Wallets
            .Where(w => w.IsPreferred && w.Status == WalletStatus.Approved && !w.IsDeleted)
            .Select(w => w.MemberId);

        var candidates = await _db.CommissionEarnings
            .Where(e => e.Status == CommissionEarningStatus.Pending && !e.IsDeleted
                        && !membersWithWallet.Contains(e.BeneficiaryMemberId))
            .GroupBy(e => e.BeneficiaryMemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.Amount) })
            .Where(x => x.Total >= minTotal)
            .OrderByDescending(x => x.Total)
            .Take(count)
            .ToListAsync(ct);

        var now = _dateTime.Now;
        var i = 0;
        foreach (var c in candidates)
        {
            var wt = activeTypes[i % activeTypes.Count];
            _db.Wallets.Add(new MemberProfilesWallet
            {
                MemberId          = c.MemberId,
                WalletType        = wt,
                Status            = WalletStatus.Approved,
                IsPreferred       = true,
                AccountIdentifier = $"{c.MemberId}@payout.dev",
                IsDeleted         = false,
                CreationDate      = now,
                CreatedBy         = "dev-seed-payout-wallets",
                LastUpdateDate    = now
            });
            i++;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<SeedPayoutWalletsResultDto>.Ok(new SeedPayoutWalletsResultDto(
            candidates.Count, activeTypes.Count,
            $"Created {candidates.Count} approved preferred payout wallets across {activeTypes.Count} active gateways.")));
    }

    public record SeedPayoutWalletsRequest(int? Count, decimal? MinTotal);
    public record SeedPayoutWalletsResultDto(int WalletsCreated, int ActiveGateways, string Message);

    public record SeedRequest(string Email, string Password);
    public record ActivateUserRequest(string Email);
    public record SeedResultDto(string Message);
    public record FixMembershipResultDto(int Activated, int DatesFixed, string Message);
    public record FixMemberOrdersResultDto(int Fixed, string Message);
    public record ForcePlaceResultDto(int Placed, string Message);
    public record RespaceRankResultDto(int GroupsFixed, int RowsAdjusted, string Message);
    public record PlaceBulkRequest(List<string>? SponsorIds);
}
