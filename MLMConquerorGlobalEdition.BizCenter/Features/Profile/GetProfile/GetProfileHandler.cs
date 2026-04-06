using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetProfile;

public class GetProfileHandler : IRequestHandler<GetProfileQuery, Result<ProfileDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetProfileHandler(AppDbContext db, ICurrentUserService currentUser, ICacheService cache)
    {
        _db          = db;
        _currentUser = currentUser;
        _cache       = cache;
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

        var dto = new ProfileDto
        {
            MemberId        = member.MemberId,
            FirstName       = member.FirstName,
            LastName        = member.LastName,
            Email           = _currentUser.Email,
            Phone           = member.Phone,
            WhatsApp        = member.WhatsApp,
            Country         = member.Country,
            State           = member.State,
            City            = member.City,
            BusinessName    = member.BusinessName,
            PhotoUrl        = member.ProfilePhotoUrl,
            MemberType      = member.MemberType.ToString(),
            Status          = member.Status.ToString(),
            EnrollDate      = member.EnrollDate,
            SponsorMemberId = member.SponsorMemberId
        };

        await _cache.SetAsync(cacheKey, dto, CacheKeys.MemberProfileTtl, ct);

        return Result<ProfileDto>.Success(dto);
    }
}
