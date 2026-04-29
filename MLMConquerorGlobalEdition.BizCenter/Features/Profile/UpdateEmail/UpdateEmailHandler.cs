using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateEmail;

public class UpdateEmailHandler : IRequestHandler<UpdateEmailCommand, Result<bool>>
{
    private readonly AppDbContext                 _db;
    private readonly ICurrentUserService          _currentUser;
    private readonly IDateTimeProvider            _dateTime;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor         _httpContext;

    public UpdateEmailHandler(
        AppDbContext                 db,
        ICurrentUserService          currentUser,
        IDateTimeProvider            dateTime,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor         httpContext)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _userManager = userManager;
        _httpContext = httpContext;
    }

    public async Task<Result<bool>> Handle(UpdateEmailCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var newEmail = command.Request.NewEmail?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newEmail))
            return Result<bool>.Failure("INVALID_EMAIL", "Email is required.");

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.MemberProfileId == memberId, ct);

        if (user is null)
            return Result<bool>.Failure("USER_NOT_FOUND", "User account not found.");

        // Reject duplicates so the unique-email constraint never blows up at the DB.
        var taken = await _userManager.Users
            .AnyAsync(u => u.Id != user.Id && u.NormalizedEmail == newEmail.ToUpperInvariant(), ct);
        if (taken)
            return Result<bool>.Failure("EMAIL_TAKEN", "That email is already registered.");

        var oldEmail = user.Email ?? string.Empty;
        if (string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Failure("EMAIL_UNCHANGED", "The new email matches your current email.");

        var setRes = await _userManager.SetEmailAsync(user, newEmail);
        if (!setRes.Succeeded)
            return Result<bool>.Failure("IDENTITY_ERROR",
                string.Join("; ", setRes.Errors.Select(e => e.Description)));

        // Username often mirrors the email — keep it in sync.
        await _userManager.SetUserNameAsync(user, newEmail);

        // Mirror the change on the profile shadow copy used by services that
        // don't want to round-trip through Identity.
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        if (member is not null)
        {
            member.Email          = newEmail;
            member.LastUpdateDate = _dateTime.UtcNow;
            member.LastUpdateBy   = _currentUser.UserId;
        }

        _db.MemberCredentialChangeLogs.Add(new MemberCredentialChangeLog
        {
            MemberId      = memberId,
            Kind          = CredentialChangeKind.Email,
            PreviousValue = oldEmail,
            NewValue      = newEmail,
            IpAddress     = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent     = _httpContext.HttpContext?.Request.Headers.UserAgent.ToString(),
            CreationDate  = _dateTime.UtcNow,
            CreatedBy     = _currentUser.UserId
        });

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
