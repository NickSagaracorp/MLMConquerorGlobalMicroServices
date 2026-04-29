using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdatePhoto;

public class UpdatePhotoHandler : IRequestHandler<UpdatePhotoCommand, Result<string>>
{
    // 5 MB cap — bigger photos get rejected before we ship them across the wire.
    private const int MaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;
    private readonly IS3FileService      _s3;
    private readonly ICacheService       _cache;

    public UpdatePhotoHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   dateTime,
        IS3FileService      s3,
        ICacheService       cache)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _s3          = s3;
        _cache       = cache;
    }

    public async Task<Result<string>> Handle(UpdatePhotoCommand command, CancellationToken ct)
    {
        var memberId    = _currentUser.MemberId;
        var contentType = string.IsNullOrWhiteSpace(command.Request.ContentType)
            ? "image/jpeg" : command.Request.ContentType.Trim().ToLowerInvariant();

        if (!AllowedMimeTypes.Contains(contentType))
            return Result<string>.Failure("UNSUPPORTED_IMAGE_TYPE",
                "Photo must be a JPEG, PNG, or WEBP image.");

        // Strip optional "data:image/...;base64," prefix the browser may include.
        var raw = command.Request.Base64Image ?? string.Empty;
        var commaIdx = raw.IndexOf(',');
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx > 0)
            raw = raw[(commaIdx + 1)..];

        byte[] bytes;
        try { bytes = Convert.FromBase64String(raw); }
        catch (FormatException)
        {
            return Result<string>.Failure("INVALID_BASE64",
                "The submitted image is not valid base64.");
        }

        if (bytes.Length == 0)
            return Result<string>.Failure("EMPTY_IMAGE", "The submitted image is empty.");

        if (bytes.Length > MaxBytes)
            return Result<string>.Failure("IMAGE_TOO_LARGE",
                $"Photo exceeds the 5 MB limit (got {bytes.Length / 1024 / 1024} MB).");

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return Result<string>.Failure("MEMBER_NOT_FOUND", "Member profile not found.");

        var extension = contentType switch
        {
            "image/png"  => "png",
            "image/webp" => "webp",
            _            => "jpg"
        };
        var s3Key = $"profile-photos/{memberId}/{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";

        using var stream = new MemoryStream(bytes);
        var publicUrl = await _s3.UploadAsync(s3Key, stream, contentType, ct);

        // Local-dev fallback when AWS is not wired: keep the previous behaviour so
        // the rest of the profile flow keeps working without a real bucket.
        if (string.IsNullOrEmpty(publicUrl))
            publicUrl = $"/profile-photos/{memberId}.{extension}";

        member.ProfilePhotoUrl = publicUrl;
        member.LastUpdateDate  = _dateTime.UtcNow;
        member.LastUpdateBy    = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.MemberProfile(memberId), ct);

        return Result<string>.Success(publicUrl);
    }
}
