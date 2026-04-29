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

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdatePassword;

public class UpdatePasswordHandler : IRequestHandler<UpdatePasswordCommand, Result<bool>>
{
    private readonly AppDbContext                 _db;
    private readonly ICurrentUserService          _currentUser;
    private readonly IDateTimeProvider            _dateTime;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor         _httpContext;

    public UpdatePasswordHandler(
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

    public async Task<Result<bool>> Handle(UpdatePasswordCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req      = command.Request;

        if (string.IsNullOrWhiteSpace(req.CurrentPassword) ||
            string.IsNullOrWhiteSpace(req.NewPassword))
            return Result<bool>.Failure("INVALID_INPUT", "Current and new password are required.");

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.MemberProfileId == memberId, ct);

        if (user is null)
            return Result<bool>.Failure("USER_NOT_FOUND", "User account not found.");

        var changeRes = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!changeRes.Succeeded)
            return Result<bool>.Failure("PASSWORD_CHANGE_REJECTED",
                string.Join("; ", changeRes.Errors.Select(e => e.Description)));

        // Log the change without ever touching the password itself.
        _db.MemberCredentialChangeLogs.Add(new MemberCredentialChangeLog
        {
            MemberId      = memberId,
            Kind          = CredentialChangeKind.Password,
            PreviousValue = null,
            NewValue      = null,
            IpAddress     = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent     = _httpContext.HttpContext?.Request.Headers.UserAgent.ToString(),
            CreationDate  = _dateTime.UtcNow,
            CreatedBy     = _currentUser.UserId
        });

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
