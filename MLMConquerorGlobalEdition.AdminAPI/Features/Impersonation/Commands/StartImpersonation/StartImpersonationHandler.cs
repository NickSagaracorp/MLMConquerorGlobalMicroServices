using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StartImpersonation;

public class StartImpersonationHandler
    : IRequestHandler<StartImpersonationCommand, Result<StartImpersonationResult>>
{
    private readonly AppDbContext                 _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService                  _jwt;
    private readonly IDateTimeProvider            _dateTime;
    private readonly ILogger<StartImpersonationHandler> _logger;

    public StartImpersonationHandler(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IJwtService jwt,
        IDateTimeProvider dateTime,
        ILogger<StartImpersonationHandler> logger)
    {
        _db          = db;
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
        _logger      = logger;
    }

    public async Task<Result<StartImpersonationResult>> Handle(
        StartImpersonationCommand command, CancellationToken ct)
    {
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == command.TargetMemberId, ct);

        if (member is null)
        {
            _logger.LogWarning(
                "Impersonation attempt by admin {AdminUserId} — member {TargetMemberId} not found.",
                command.AdminUserId, command.TargetMemberId);

            return Result<StartImpersonationResult>.Failure(
                "MEMBER_NOT_FOUND",
                $"Member '{command.TargetMemberId}' not found.");
        }

        var targetUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.MemberProfileId == member.Id, ct);

        if (targetUser is null)
        {
            _logger.LogWarning(
                "Impersonation attempt by admin {AdminUserId} — member {TargetMemberId} has no linked user account.",
                command.AdminUserId, command.TargetMemberId);

            return Result<StartImpersonationResult>.Failure(
                "MEMBER_HAS_NO_USER_ACCOUNT",
                $"Member '{command.TargetMemberId}' does not have an associated user account.");
        }

        // SupportManager without SuperAdmin/Admin gets read-only access
        var isReadOnly = command.AdminRoles.Contains("SupportManager")
                      && !command.AdminRoles.Contains("SuperAdmin")
                      && !command.AdminRoles.Contains("Admin");

        var targetRoles = await _userManager.GetRolesAsync(targetUser);

        // Impersonation tokens have a fixed 2-hour expiry
        var expiresAt = _dateTime.Now.AddHours(2);

        var accessToken = _jwt.GenerateAccessToken(
            userId:          targetUser.Id,
            memberId:        command.TargetMemberId,
            email:           targetUser.Email ?? string.Empty,
            roles:           targetRoles,
            isImpersonating: true,
            impersonatedBy:  command.AdminUserId);

        var memberName = $"{member.FirstName} {member.LastName}".Trim();

        _logger.LogInformation(
            "Admin {AdminUserId} started impersonation of member {TargetMemberId} (ReadOnly={IsReadOnly}). Token expires at {ExpiresAt}.",
            command.AdminUserId, command.TargetMemberId, isReadOnly, expiresAt);

        return Result<StartImpersonationResult>.Success(new StartImpersonationResult(
            AccessToken: accessToken,
            MemberId:    command.TargetMemberId,
            MemberName:  memberName,
            IsReadOnly:  isReadOnly,
            ExpiresAt:   expiresAt));
    }
}
