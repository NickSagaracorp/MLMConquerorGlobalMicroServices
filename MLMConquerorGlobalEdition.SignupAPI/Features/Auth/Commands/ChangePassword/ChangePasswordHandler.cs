using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ChangePassword;

/// <summary>
/// Cambia la contraseña de una cuenta que ya tiene una, y deja constancia en el historial de
/// credenciales del miembro.
/// </summary>
/// <remarks>
/// EL HISTORIAL SUBIÓ AQUÍ AL UNIFICAR EL CAMBIO DE CONTRASEÑA. El centro de negocios tenía su
/// propio formulario posteando a <c>PUT /api/v1/bizcenter/profile/credentials/password</c>, y
/// aquel manejador —además de cambiar la contraseña— escribía una fila en
/// <c>MemberCredentialChangeLogs</c>. Ese registro no es decorativo: es lo que pinta
/// <c>CredentialsHistoryTable</c>, que el miembro tiene en la misma pestaña, y es de donde sale
/// la pista de un cambio de contraseña que uno no hizo. Mover la pantalla al componente
/// compartido sin traerse esta escritura habría dejado ese historial mudo justo para el suceso
/// que más importa vigilar.
///
/// SOLO PARA CUENTAS CON PERFIL DE MIEMBRO. La tabla se indexa por <c>MemberId</c> y el personal
/// interno no tiene ninguno; para ellos no hay fila que escribir ni pantalla donde leerla. Ese
/// identificador se toma del <c>ApplicationUser</c> que este manejador ya cargó y no de
/// <c>ICurrentUserService.MemberId</c>, que lee el claim <c>member_id</c> mientras el token emite
/// <c>memberId</c> y por tanto devuelve vacío aquí.
///
/// LA FECHA NO SE PONE AQUÍ, y no es un olvido: <c>AuditInterceptor</c> sella
/// <c>CreationDate</c> con la hora del servidor en todo lo que hereda de
/// <c>AuditChangesLongKey</c> justo antes de guardar, así que cualquier valor escrito en este
/// manejador se perdería sin dejar rastro. Escribirlo igualmente —como hace el manejador de
/// BizCenter, que además lo pone en UtcNow— es código muerto que hace creer que esa decisión se
/// toma aquí.
/// </remarks>
public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext                 _db;
    private readonly IHttpContextAccessor         _httpContext;

    public ChangePasswordHandler(
        UserManager<ApplicationUser> userManager,
        AppDbContext                 db,
        IHttpContextAccessor         httpContext)
    {
        _userManager = userManager;
        _db          = db;
        _httpContext = httpContext;
    }

    public async Task<Result<bool>> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        var req  = command.Request;
        var user = await _userManager.FindByIdAsync(command.UserId);

        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Failure("PASSWORD_CHANGE_FAILED", errors);
        }

        // Invalidate refresh tokens on password change
        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        await LogCredentialChangeAsync(user, ct);

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Anota el cambio en el historial de credenciales del miembro, sin tocar nunca la contraseña
    /// misma: <c>PreviousValue</c> y <c>NewValue</c> se quedan nulos a propósito — para un cambio
    /// de contraseña lo único que se guarda es que ocurrió, cuándo y desde dónde.
    /// </summary>
    private async Task LogCredentialChangeAsync(ApplicationUser user, CancellationToken ct)
    {
        var memberId = user.MemberProfileId;
        if (string.IsNullOrWhiteSpace(memberId))
            return;

        _db.MemberCredentialChangeLogs.Add(new MemberCredentialChangeLog
        {
            MemberId      = memberId,
            Kind          = CredentialChangeKind.Password,
            PreviousValue = null,
            NewValue      = null,
            IpAddress     = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent     = _httpContext.HttpContext?.Request.Headers.UserAgent.ToString(),
            CreatedBy     = user.Id
        });

        await _db.SaveChangesAsync(ct);
    }
}
