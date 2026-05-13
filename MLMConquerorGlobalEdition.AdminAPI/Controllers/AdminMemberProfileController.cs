using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Admin-side mirror of the BizCenter Profile + Billing read/edit surface.
///
/// Powers the unified profile UI (<c>SharedComponents/Components/Profile/UserProfilePage</c>)
/// when it is rendered inside AdminWeb at <c>/admin/members/{memberId}</c>. The shape of
/// every response matches its BizCenter counterpart exactly so the same UI binds against
/// either base path. The shared component flips between
/// <c>api/v1/bizcenter/...</c> (member view) and <c>api/v1/admin/members/{memberId}/...</c>
/// (admin view) using <c>IViewContextService.IsAdminContext</c>.
///
/// Sensitive operations (password / email / 2FA / replicate-site / credit-card writes)
/// are intentionally NOT mirrored here. Admins go through Identity-side flows or support
/// tickets for those; the UI hides those entry points when in admin context.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager,SupportLevel1,SupportLevel2,SupportLevel3")]
public class AdminMemberProfileController : ControllerBase
{
    private readonly AppDbContext             _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDateTimeProvider         _clock;
    private readonly ICacheService             _cache;
    private readonly ICurrentUserService       _currentUser;
    private readonly IHttpContextAccessor      _httpContext;

    public AdminMemberProfileController(
        AppDbContext              db,
        UserManager<ApplicationUser> userManager,
        IDateTimeProvider         clock,
        ICacheService             cache,
        ICurrentUserService       currentUser,
        IHttpContextAccessor      httpContext)
    {
        _db          = db;
        _userManager = userManager;
        _clock       = clock;
        _cache       = cache;
        _currentUser = currentUser;
        _httpContext = httpContext;
    }

    // ─── Profile read ──────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/members/{memberId}/profile — same shape as BizCenter's GET /profile.</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(string memberId, CancellationToken ct = default)
    {
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return NotFound(ApiResponse<ProfileResponse>.Fail("MEMBER_NOT_FOUND", "Member profile not found."));

        // The member's login email lives on ApplicationUser, not on MemberProfile.
        // Fall back to MemberProfile.Email (the seed value at signup) if no Identity user exists yet.
        var appUser = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.MemberProfileId == memberId)
            .Select(u => new { u.Email })
            .FirstOrDefaultAsync(ct);

        var activeSub = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => s.MemberId == memberId && !s.IsDeleted)
            .OrderByDescending(s => s.SubscriptionStatus == MembershipStatus.Active ? 1 : 0)
            .ThenByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);

        var dto = new ProfileResponse
        {
            MemberId          = member.MemberId,
            FirstName         = member.FirstName,
            LastName          = member.LastName,
            DateOfBirth       = member.DateOfBirth,
            BusinessName      = member.BusinessName,
            // Tax IDs are encrypted with BizCenter's DataProtection key ring; admins
            // intentionally do not see SSN/EIN last-4 here. The PersonalInfoCard
            // renders "Not on file" when these are null.
            SsnLast4          = null,
            EinLast4          = null,

            Email             = appUser?.Email ?? member.Email,
            ReplicateSiteSlug = member.ReplicateSiteSlug,
            PhotoUrl          = member.ProfilePhotoUrl,

            Phone             = member.Phone,
            WhatsApp          = member.WhatsApp,

            Country           = member.Country,
            State             = member.State,
            City              = member.City,
            Address           = member.Address,
            ZipCode           = member.ZipCode,

            DefaultLanguage   = string.IsNullOrEmpty(member.DefaultLanguage) ? "en" : member.DefaultLanguage,
            PayoutFrequency   = member.PayoutFrequency.ToString(),

            ShowBusinessName  = member.ShowBusinessName,
            IsEmailPublic     = member.IsEmailPublic,
            IsPhonePublic     = member.IsPhonePublic,

            MemberType        = member.MemberType.ToString(),
            Status            = member.Status.ToString(),
            EnrollDate        = member.EnrollDate,
            SponsorMemberId   = member.SponsorMemberId,

            Membership        = activeSub is null ? null : new MembershipSnapshotResponse
            {
                LevelId     = activeSub.MembershipLevelId,
                LevelName   = activeSub.MembershipLevel?.Name ?? string.Empty,
                Status      = activeSub.SubscriptionStatus.ToString(),
                StartDate   = activeSub.StartDate,
                ExpireDate  = activeSub.EndDate ?? activeSub.RenewalDate,
                IsAutoRenew = activeSub.IsAutoRenew
            }
        };

        return Ok(ApiResponse<ProfileResponse>.Ok(dto));
    }

    // ─── Profile edit ──────────────────────────────────────────────────────────

    /// <summary>PUT /api/v1/admin/members/{memberId}/profile — admin edits address/preferences.
    /// Sensitive identity fields (name, DOB, SSN, EIN) remain locked just like in BizCenter.</summary>
    [Authorize(Roles = "SuperAdmin,Admin,SupportManager")]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        string memberId,
        [FromBody] AdminUpdateProfileRequest req,
        CancellationToken ct = default)
    {
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        if (member is null)
            return NotFound(ApiResponse<bool>.Fail("MEMBER_NOT_FOUND", "Member profile not found."));

        var prevAddress = member.Address;
        var prevCity    = member.City;
        var prevState   = member.State;
        var prevZip     = member.ZipCode;
        var prevCountry = member.Country;

        member.Phone    = req.Phone;
        member.WhatsApp = req.WhatsApp;
        member.Country  = req.Country ?? member.Country;
        member.State    = req.State;
        member.City     = req.City;
        member.Address  = req.Address;
        member.ZipCode  = req.ZipCode;

        if (!string.IsNullOrWhiteSpace(req.DefaultLanguage))
            member.DefaultLanguage = req.DefaultLanguage.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(req.PayoutFrequency)
            && Enum.TryParse<PayoutFrequency>(req.PayoutFrequency, ignoreCase: true, out var pf))
            member.PayoutFrequency = pf;

        member.ShowBusinessName = req.ShowBusinessName;
        member.IsEmailPublic    = req.IsEmailPublic;
        member.IsPhonePublic    = req.IsPhonePublic;

        member.LastUpdateDate = _clock.Now;
        member.LastUpdateBy   = _currentUser.UserId;

        var addressChanged =
            !string.Equals(prevAddress, member.Address, StringComparison.Ordinal) ||
            !string.Equals(prevCity,    member.City,    StringComparison.Ordinal) ||
            !string.Equals(prevState,   member.State,   StringComparison.Ordinal) ||
            !string.Equals(prevZip,     member.ZipCode, StringComparison.Ordinal) ||
            !string.Equals(prevCountry, member.Country, StringComparison.Ordinal);

        if (addressChanged)
        {
            // Tag the actor field so the audit trail shows the admin who made the change,
            // not just the member id. Keeps support investigations readable.
            var adminActor = _currentUser.UserId;
            var reason     = string.IsNullOrWhiteSpace(req.AddressChangeReason)
                ? $"[admin edit by {adminActor}]"
                : $"[admin edit by {adminActor}] {req.AddressChangeReason}";

            _db.MemberAddressHistories.Add(new MemberAddressHistory
            {
                MemberId        = memberId,
                PreviousAddress = prevAddress,
                PreviousCity    = prevCity,
                PreviousState   = prevState,
                PreviousZipCode = prevZip,
                PreviousCountry = prevCountry,
                NewAddress      = member.Address,
                NewCity         = member.City,
                NewState        = member.State,
                NewZipCode      = member.ZipCode,
                NewCountry      = member.Country,
                Reason          = reason,
                IpAddress       = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent       = _httpContext.HttpContext?.Request.Headers.UserAgent.ToString(),
                CreationDate    = _clock.Now,
                CreatedBy       = adminActor
            });
        }

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MemberProfile(memberId), ct);

        // Echo back the fresh shape — call the same GET path so we never diverge.
        return await GetProfile(memberId, ct);
    }

    /// <summary>PUT /api/v1/admin/members/{memberId}/profile/photo — admin updates photo URL.</summary>
    [Authorize(Roles = "SuperAdmin,Admin,SupportManager")]
    [HttpPut("profile/photo")]
    public async Task<IActionResult> UpdatePhoto(
        string memberId,
        [FromBody] AdminUpdatePhotoRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.PhotoUrl))
            return BadRequest(ApiResponse<string>.Fail("INVALID_PHOTO", "Photo URL is required."));

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        if (member is null)
            return NotFound(ApiResponse<string>.Fail("MEMBER_NOT_FOUND", "Member profile not found."));

        member.ProfilePhotoUrl = req.PhotoUrl;
        member.LastUpdateDate  = _clock.Now;
        member.LastUpdateBy    = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MemberProfile(memberId), ct);

        return Ok(ApiResponse<string>.Ok(req.PhotoUrl, "Profile photo updated."));
    }

    // ─── Audit / history reads ─────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/members/{memberId}/profile/security-log — paged audit trail for the member.</summary>
    [HttpGet("profile/security-log")]
    public async Task<IActionResult> GetSecurityLog(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // The member's own user id is what GeneralAuditTracking.ChangedBy stamps for self-edits.
        var ownerUserId = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.MemberProfileId == memberId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        var query = _db.AuditTracking
            .AsNoTracking()
            .Where(a => ownerUserId != null && a.ChangedBy == ownerUserId)
            .OrderByDescending(a => a.ChangedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new SecurityLogResponse
            {
                EventType  = $"{a.Action} {a.EntityName}",
                IpAddress  = "—",
                OccurredAt = a.ChangedAt,
                Status     = "Success"
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<SecurityLogResponse>>.Ok(new PagedResult<SecurityLogResponse>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    /// <summary>GET /api/v1/admin/members/{memberId}/profile/address-history — paged change log.</summary>
    [HttpGet("profile/address-history")]
    public async Task<IActionResult> GetAddressHistory(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.MemberAddressHistories
            .AsNoTracking()
            .Where(h => h.MemberId == memberId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(h => h.CreationDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(h => new AddressHistoryResponse
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

        return Ok(ApiResponse<PagedResult<AddressHistoryResponse>>.Ok(new PagedResult<AddressHistoryResponse>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    /// <summary>GET /api/v1/admin/members/{memberId}/profile/credentials-history — email/password/2FA changes.</summary>
    [HttpGet("profile/credentials-history")]
    public async Task<IActionResult> GetCredentialsHistory(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.MemberCredentialChangeLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreationDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new CredentialChangeResponse
            {
                Id        = l.Id,
                ChangedAt = l.CreationDate,
                ChangedBy = l.CreatedBy,
                Kind      = l.Kind.ToString(),
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<CredentialChangeResponse>>.Ok(new PagedResult<CredentialChangeResponse>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    // ─── Billing reads ────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/members/{memberId}/billing/credit-cards — read-only list.
    /// Expiry month/year are encrypted with BizCenter's DataProtection key ring; admins see Last4/Brand/IsDefault.</summary>
    [HttpGet("billing/credit-cards")]
    public async Task<IActionResult> GetCreditCards(string memberId, CancellationToken ct = default)
    {
        var rows = await _db.CreditCards
            .AsNoTracking()
            .Where(c => c.MemberId == memberId && !c.IsDeleted)
            .OrderBy(c => c.Priority)
            .ThenByDescending(c => c.CreationDate)
            .Select(c => new CreditCardResponse
            {
                Id               = c.Id,
                Last4            = c.Last4,
                First6           = c.First6,
                CardBrand        = c.CardBrand,
                ExpiryMonth      = 0,
                ExpiryYear       = 0,
                IsDefault        = c.IsDefault,
                IsExpired        = c.IsExpired,
                Priority         = c.Priority,
                MaskedCardNumber = c.MaskedCardNumber
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<CreditCardResponse>>.Ok(rows));
    }

    /// <summary>GET /api/v1/admin/members/{memberId}/billing/history — paged order history.</summary>
    [HttpGet("billing/history")]
    public async Task<IActionResult> GetBillingHistory(
        string memberId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Orders
            .AsNoTracking()
            .Where(o => o.MemberId == memberId)
            .OrderByDescending(o => o.OrderDate);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderHistoryResponse
            {
                OrderId     = o.Id,
                OrderDate   = o.OrderDate,
                TotalAmount = o.TotalAmount,
                Status      = o.Status.ToString(),
                Notes       = o.Notes
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<OrderHistoryResponse>>.Ok(new PagedResult<OrderHistoryResponse>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    // ─── Local DTOs (JSON shape mirrors BizCenter exactly) ────────────────────

    public class ProfileResponse
    {
        public string   MemberId          { get; set; } = string.Empty;
        public string   FirstName         { get; set; } = string.Empty;
        public string   LastName          { get; set; } = string.Empty;
        public DateTime DateOfBirth       { get; set; }
        public string?  BusinessName      { get; set; }
        public string?  SsnLast4          { get; set; }
        public string?  EinLast4          { get; set; }
        public string   Email             { get; set; } = string.Empty;
        public string?  ReplicateSiteSlug { get; set; }
        public string?  PhotoUrl          { get; set; }
        public string?  Phone             { get; set; }
        public string?  WhatsApp          { get; set; }
        public string?  Country           { get; set; }
        public string?  State             { get; set; }
        public string?  City              { get; set; }
        public string?  Address           { get; set; }
        public string?  ZipCode           { get; set; }
        public string   DefaultLanguage   { get; set; } = "en";
        public string   PayoutFrequency   { get; set; } = "Weekly";
        public bool     ShowBusinessName  { get; set; }
        public bool     IsEmailPublic     { get; set; }
        public bool     IsPhonePublic     { get; set; }
        public string   MemberType        { get; set; } = string.Empty;
        public string   Status            { get; set; } = string.Empty;
        public DateTime EnrollDate        { get; set; }
        public string?  SponsorMemberId   { get; set; }
        public MembershipSnapshotResponse? Membership { get; set; }
    }

    public class MembershipSnapshotResponse
    {
        public int       LevelId     { get; set; }
        public string    LevelName   { get; set; } = string.Empty;
        public string    Status      { get; set; } = string.Empty;
        public DateTime  StartDate   { get; set; }
        public DateTime? ExpireDate  { get; set; }
        public bool      IsAutoRenew { get; set; }
    }

    public class AdminUpdateProfileRequest
    {
        public string?  Phone               { get; set; }
        public string?  WhatsApp            { get; set; }
        public string?  Country             { get; set; }
        public string?  State               { get; set; }
        public string?  City                { get; set; }
        public string?  Address             { get; set; }
        public string?  ZipCode             { get; set; }
        public string?  AddressChangeReason { get; set; }
        public string?  DefaultLanguage     { get; set; }
        public string?  PayoutFrequency     { get; set; }
        public bool     ShowBusinessName    { get; set; }
        public bool     IsEmailPublic       { get; set; }
        public bool     IsPhonePublic       { get; set; }
    }

    public class AdminUpdatePhotoRequest
    {
        public string PhotoUrl { get; set; } = string.Empty;
    }

    public class SecurityLogResponse
    {
        public string   EventType  { get; set; } = string.Empty;
        public string   IpAddress  { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string   Status     { get; set; } = string.Empty;
    }

    public class AddressHistoryResponse
    {
        public long     Id              { get; set; }
        public DateTime ChangedAt       { get; set; }
        public string?  ChangedBy       { get; set; }
        public string?  PreviousAddress { get; set; }
        public string?  PreviousCity    { get; set; }
        public string?  PreviousState   { get; set; }
        public string?  PreviousZipCode { get; set; }
        public string?  PreviousCountry { get; set; }
        public string?  NewAddress      { get; set; }
        public string?  NewCity         { get; set; }
        public string?  NewState        { get; set; }
        public string?  NewZipCode      { get; set; }
        public string?  NewCountry      { get; set; }
        public string?  Reason          { get; set; }
    }

    public class CredentialChangeResponse
    {
        public long     Id        { get; set; }
        public DateTime ChangedAt { get; set; }
        public string?  ChangedBy { get; set; }
        public string   Kind      { get; set; } = string.Empty;
        public string?  IpAddress { get; set; }
        public string?  UserAgent { get; set; }
    }

    public class CreditCardResponse
    {
        public string Id               { get; set; } = string.Empty;
        public string Last4            { get; set; } = string.Empty;
        public string First6           { get; set; } = string.Empty;
        public string CardBrand        { get; set; } = string.Empty;
        public int    ExpiryMonth      { get; set; }
        public int    ExpiryYear       { get; set; }
        public bool   IsDefault        { get; set; }
        public bool   IsExpired        { get; set; }
        public int    Priority         { get; set; }
        public string MaskedCardNumber { get; set; } = string.Empty;
    }

    public class OrderHistoryResponse
    {
        public string   OrderId     { get; set; } = string.Empty;
        public DateTime OrderDate   { get; set; }
        public decimal  TotalAmount { get; set; }
        public string   Status      { get; set; } = string.Empty;
        public string?  Notes       { get; set; }
    }
}
