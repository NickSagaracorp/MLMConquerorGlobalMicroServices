using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IEncryptionService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEncryptionService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetProfile;

public class GetProfileHandler : IRequestHandler<GetProfileQuery, Result<ProfileDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService       _cache;
    private readonly IEncryptionService  _encryption;

    public GetProfileHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        ICacheService       cache,
        IEncryptionService  encryption)
    {
        _db          = db;
        _currentUser = currentUser;
        _cache       = cache;
        _encryption  = encryption;
    }

    public async Task<Result<ProfileDto>> Handle(GetProfileQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var cacheKey = CacheKeys.MemberProfile(memberId);

        var cached = await _cache.GetAsync<ProfileDto>(cacheKey, ct);
        if (cached is not null)
            return Result<ProfileDto>.Success(cached);

        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return Result<ProfileDto>.Failure("MEMBER_NOT_FOUND", "Member profile not found.");

        // Active membership snapshot — most recent active subscription wins, with
        // the level name joined in for display. We don't fail if there is none.
        var activeSub = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => s.MemberId == memberId && !s.IsDeleted)
            .OrderByDescending(s =>
                s.SubscriptionStatus == MembershipStatus.Active ? 1 : 0)
            .ThenByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);

        var dto = new ProfileDto
        {
            MemberId          = member.MemberId,
            FirstName         = member.FirstName,
            LastName          = member.LastName,
            DateOfBirth       = member.DateOfBirth,
            BusinessName      = member.BusinessName,
            SsnLast4          = TryDecryptLast4(member.SsnEncrypted),
            EinLast4          = TryDecryptLast4(member.EinEncrypted),

            Email             = _currentUser.Email,
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

            Membership        = activeSub is null ? null : new MembershipSnapshotDto
            {
                LevelId     = activeSub.MembershipLevelId,
                LevelName   = activeSub.MembershipLevel?.Name ?? string.Empty,
                Status      = activeSub.SubscriptionStatus.ToString(),
                StartDate   = activeSub.StartDate,
                ExpireDate  = activeSub.EndDate ?? activeSub.RenewalDate,
                IsAutoRenew = activeSub.IsAutoRenew
            }
        };

        await _cache.SetAsync(cacheKey, dto, CacheKeys.MemberProfileTtl, ct);
        return Result<ProfileDto>.Success(dto);
    }

    /// <summary>
    /// Decrypts a tax-ID ciphertext and returns just the last 4 digits for display.
    /// Used by both SSN ("***-**-1234") and EIN ("**-***1234"). Never returns the full ID.
    /// </summary>
    private string? TryDecryptLast4(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext)) return null;
        try
        {
            var plain = _encryption.Decrypt(ciphertext);
            // Strip non-digits in case the value was stored with hyphens (e.g. EIN "12-3456789").
            var digits = new string(plain.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : digits;
        }
        catch { return null; }
    }
}
