using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetProfile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using PayoutFrequency = MLMConquerorGlobalEdition.Domain.Enums.PayoutFrequency;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateProfile;

/// <summary>
/// Updates the editable subset of the member's profile. Identity fields
/// (FirstName, LastName, DateOfBirth, BusinessName, SSN, EIN) are intentionally
/// left untouched — those are read-only by design (account-takeover defense).
/// Any change to the address writes a row to <c>MemberAddressHistories</c>.
/// </summary>
public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;
    private readonly ICacheService       _cache;
    private readonly IMediator           _mediator;
    private readonly IHttpContextAccessor _httpContext;

    public UpdateProfileHandler(
        AppDbContext         db,
        ICurrentUserService  currentUser,
        IDateTimeProvider    dateTime,
        ICacheService        cache,
        IMediator            mediator,
        IHttpContextAccessor httpContext)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _cache       = cache;
        _mediator    = mediator;
        _httpContext = httpContext;
    }

    public async Task<Result<ProfileDto>> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req      = command.Request;

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return Result<ProfileDto>.Failure("MEMBER_NOT_FOUND", "Member profile not found.");

        // ── Snapshot the address BEFORE we mutate, so we can detect changes ──
        var prevAddress = member.Address;
        var prevCity    = member.City;
        var prevState   = member.State;
        var prevZip     = member.ZipCode;
        var prevCountry = member.Country;

        // ── Contact ──
        member.Phone    = req.Phone;
        member.WhatsApp = req.WhatsApp;

        // ── Address ──
        member.Country = req.Country ?? member.Country;
        member.State   = req.State;
        member.City    = req.City;
        member.Address = req.Address;
        member.ZipCode = req.ZipCode;

        // ── Preferences ──
        if (!string.IsNullOrWhiteSpace(req.DefaultLanguage))
            member.DefaultLanguage = req.DefaultLanguage.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(req.PayoutFrequency)
            && Enum.TryParse<PayoutFrequency>(req.PayoutFrequency, ignoreCase: true, out var pf))
            member.PayoutFrequency = pf;

        // ── Public-page visibility ──
        member.ShowBusinessName = req.ShowBusinessName;
        member.IsEmailPublic    = req.IsEmailPublic;
        member.IsPhonePublic    = req.IsPhonePublic;

        member.LastUpdateDate = _dateTime.UtcNow;
        member.LastUpdateBy   = _currentUser.UserId;

        // ── Address change → history row ──
        var addressChanged =
            !string.Equals(prevAddress, member.Address, StringComparison.Ordinal) ||
            !string.Equals(prevCity,    member.City,    StringComparison.Ordinal) ||
            !string.Equals(prevState,   member.State,   StringComparison.Ordinal) ||
            !string.Equals(prevZip,     member.ZipCode, StringComparison.Ordinal) ||
            !string.Equals(prevCountry, member.Country, StringComparison.Ordinal);

        if (addressChanged)
        {
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
                Reason          = req.AddressChangeReason,
                IpAddress       = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent       = _httpContext.HttpContext?.Request.Headers.UserAgent.ToString(),
                CreationDate    = _dateTime.UtcNow,
                CreatedBy       = _currentUser.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MemberProfile(memberId), ct);

        // Re-read through the canonical GetProfile path so we return the same
        // shape (membership snapshot, decrypted SSN last-4, etc.) the GET returns.
        return await _mediator.Send(new GetProfileQuery(), ct);
    }
}
