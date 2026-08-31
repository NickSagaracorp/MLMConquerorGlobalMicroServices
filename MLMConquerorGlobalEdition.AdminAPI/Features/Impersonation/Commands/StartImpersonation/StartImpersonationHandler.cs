using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
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
            .FirstOrDefaultAsync(u => u.MemberProfileId == member.MemberId, ct);

        if (targetUser is null)
        {
            _logger.LogWarning(
                "Impersonation attempt by admin {AdminUserId} — member {TargetMemberId} has no linked user account.",
                command.AdminUserId, command.TargetMemberId);

            return Result<StartImpersonationResult>.Failure(
                "MEMBER_HAS_NO_USER_ACCOUNT",
                $"Member '{command.TargetMemberId}' does not have an associated user account.");
        }

        var targetRoles = await _userManager.GetRolesAsync(targetUser);

        // NO SE SUPLANTA A UNA CUENTA DE PERSONAL. El token de suplantación se emite con los roles
        // DEL SUPLANTADO, así que una cuenta de miembro que además tuviera un rol de panel
        // convertiría la suplantación en una subida de privilegios: un SupportManager entraría a
        // ese miembro y saldría con los roles de ese miembro. La superficie existe para atender a
        // miembros, y un miembro no tiene roles de personal; si los tiene, es que esa cuenta no es
        // el sujeto de esta operación.
        var rolesDePersonal = targetRoles.Where(AppRoles.AdminRoles.Contains).ToList();
        if (rolesDePersonal.Count > 0)
        {
            _logger.LogWarning(
                "Impersonation attempt by admin {AdminUserId} — member {TargetMemberId} holds staff roles {StaffRoles}.",
                command.AdminUserId, command.TargetMemberId, string.Join(",", rolesDePersonal));

            return Result<StartImpersonationResult>.Failure(
                "TARGET_IS_STAFF",
                $"Member '{command.TargetMemberId}' is linked to a staff account and cannot be impersonated.");
        }

        // SupportManager without SuperAdmin/Admin gets read-only access
        var isReadOnly = command.AdminRoles.Contains(AppRoles.SupportManager)
                      && !command.AdminRoles.Contains(AppRoles.SuperAdmin)
                      && !command.AdminRoles.Contains(AppRoles.Admin);

        // Impersonation tokens have a fixed 2-hour expiry
        var expiresAt = _dateTime.Now.AddHours(2);

        // isReadOnly VIAJA EN EL TOKEN. Antes solo salía en el cuerpo de la respuesta —abajo, en
        // IsReadOnly— y eso no limitaba nada: quien usara el token contra la API directamente iba
        // con los roles completos del suplantado durante dos horas. La restricción la aplica ahora
        // el servidor que recibe la petición, en ImpersonationReadOnlyMiddleware; el campo de la
        // respuesta se queda porque la interfaz lo usa para pintarse en modo consulta, pero ya no
        // es lo único que separa a un vistazo de una escritura.
        var accessToken = _jwt.GenerateAccessToken(
            userId:                targetUser.Id,
            memberId:              command.TargetMemberId,
            email:                 targetUser.Email ?? string.Empty,
            roles:                 targetRoles,
            isImpersonating:       true,
            impersonatedBy:        command.AdminUserId,
            impersonationReadOnly: isReadOnly);

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
