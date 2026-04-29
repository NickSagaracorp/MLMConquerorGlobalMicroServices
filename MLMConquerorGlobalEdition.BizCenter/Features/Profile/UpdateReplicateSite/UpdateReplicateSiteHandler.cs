using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateReplicateSite;

public class UpdateReplicateSiteHandler : IRequestHandler<UpdateReplicateSiteCommand, Result<string>>
{
    // Lowercase letters, digits and hyphens only; 3–40 chars; must start with a letter.
    private static readonly Regex SlugPattern = new(@"^[a-z][a-z0-9\-]{2,39}$", RegexOptions.Compiled);

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;
    private readonly ICacheService       _cache;

    public UpdateReplicateSiteHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   dateTime,
        ICacheService       cache)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _cache       = cache;
    }

    public async Task<Result<string>> Handle(UpdateReplicateSiteCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var slug     = command.Request.Slug?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!SlugPattern.IsMatch(slug))
            return Result<string>.Failure("INVALID_SLUG",
                "Website name must be 3–40 characters: lowercase letters, digits, and hyphens only, and must start with a letter.");

        var taken = await _db.MemberProfiles
            .AsNoTracking()
            .AnyAsync(m => m.MemberId != memberId
                        && m.ReplicateSiteSlug == slug
                        && !m.IsDeleted, ct);

        if (taken)
            return Result<string>.Failure("SLUG_TAKEN",
                $"The website name '{slug}' is already in use. Pick another one.");

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return Result<string>.Failure("MEMBER_NOT_FOUND", "Member profile not found.");

        member.ReplicateSiteSlug = slug;
        member.LastUpdateDate    = _dateTime.UtcNow;
        member.LastUpdateBy      = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MemberProfile(memberId), ct);

        return Result<string>.Success(slug);
    }
}
