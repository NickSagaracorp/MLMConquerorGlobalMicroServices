using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Las cuentas de personal: las que no tienen <c>MemberProfile</c> detrás.
/// </summary>
/// <remarks>
/// ADMINISTRAR UNA CUENTA NO EXPULSABA A QUIEN YA ESTABA DENTRO. Las cuatro operaciones de
/// escritura de aquí cambian la postura de seguridad de una cuenta de personal —desactivarla,
/// darla de baja, cambiarle el correo (que es su identificador de acceso y el destino de su enlace
/// de recuperación) y cambiarle los roles— y ninguna tocaba el refresh token. Ese token vive
/// treinta días en <c>ApplicationUser.RefreshToken</c> y sirve para pedir tokens de acceso nuevos
/// SIN contraseña y SIN segundo factor: mientras siguiera ahí, la cuenta que se acababa de
/// desactivar seguía renovándose sola, y el usuario al que se le acababan de quitar los roles
/// seguía pidiendo tokens —eso sí, ya con los roles nuevos, porque el refresco los relee—.
///
/// LA REGLA NO SE ESCRIBE AQUÍ. Es la misma de <see cref="SessionRevocation"/> que rige desde
/// 4f4beaf en el área de cuenta de SignupAPI: se revoca cuando cambia QUÉ hace falta para entrar o
/// CON QUÉ se demuestra. Desactivar y borrar retiran el derecho a entrar; cambiar el correo cambia
/// el identificador, el canal de recuperación y el canal de 2FA que siempre está disponible;
/// cambiar los roles cambia a qué da acceso la sesión. Los cuatro casos caen del mismo lado de esa
/// línea. Guardar el mismo correo, los mismos roles y el mismo estado no revoca nada: un PUT que no
/// cambia nada no es un cambio de postura, y tratarlo como tal convertiría abrir el formulario y
/// darle a guardar en un cierre de sesión.
///
/// LO QUE ESTO NO ALCANZA, y no es un descuido sino el límite del diseño: el token de ACCESO ya
/// emitido. Es autofirmado, nadie lo consulta contra la base, y vive lo que diga
/// <c>Jwt:AccessTokenExpiryMinutes</c> —quince minutos por defecto—. Revocar aquí cierra la
/// RENOVACIÓN, así que la sesión muere como mucho al caducar ese token; hasta entonces sigue
/// entrando, y con los roles VIEJOS, porque van dentro del token. Cerrar también esa ventana exige
/// que cada petición de cada servicio consulte un estado en la base —una lista de revocación en
/// Redis, o el <c>SecurityStamp</c> de Identity validado en cada petición— y eso es una decisión de
/// arquitectura con coste por petición en los siete servicios; queda descrita en el informe y sin
/// construir.
/// </remarks>
[ApiController]
[Route("api/v1/admin/system-users")]
[Authorize(Roles = "SuperAdmin")]
public class SystemUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDateTimeProvider _dateTime;

    public SystemUsersController(UserManager<ApplicationUser> userManager, IDateTimeProvider dateTime)
    {
        _userManager = userManager;
        _dateTime = dateTime;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _userManager.Users
            .Where(u => u.MemberProfileId == null)
            .OrderByDescending(u => u.CreationDate);

        var total = await query.CountAsync(ct);
        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var dtos = new List<SystemUserDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            dtos.Add(new SystemUserDto(
                u.Id,
                u.UserName ?? u.Email ?? string.Empty,
                u.Email ?? string.Empty,
                roles.FirstOrDefault() ?? string.Empty,
                u.IsActive,
                u.LastLoginAt,
                u.CreationDate));
        }

        var result = new PagedResult<SystemUserDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResult<SystemUserDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSystemUserRequest dto, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing is not null)
            return BadRequest(ApiResponse<string>.Fail("EMAIL_TAKEN", "A user with this email already exists."));

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            IsActive = dto.IsActive,
            CreationDate = _dateTime.Now,
            CreatedBy = User.Identity?.Name ?? "admin"
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Fail("CREATE_FAILED",
                string.Join(", ", result.Errors.Select(e => e.Description))));

        if (!string.IsNullOrWhiteSpace(dto.Role))
            await _userManager.AddToRoleAsync(user, dto.Role);

        return Ok(ApiResponse<string>.Ok(user.Id, "System user created."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSystemUserRequest dto, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "User not found."));

        // Se decide ANTES de tocar la entidad: después de asignar, "cambió" ya no se puede
        // preguntar. Y los roles se leen antes por lo mismo, no solo para quitarlos.
        var existingRoles = await _userManager.GetRolesAsync(user);

        var cambiaCorreo = !string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase);
        var cambianRoles = !MismoJuegoDeRoles(existingRoles, dto.Role);
        var seDesactiva  = user.IsActive && !dto.IsActive;

        user.Email = dto.Email;
        user.UserName = dto.Email;
        user.IsActive = dto.IsActive;

        if (cambiaCorreo || cambianRoles || seDesactiva)
            user.RevokeLiveSessions();

        await _userManager.UpdateAsync(user);

        if (existingRoles.Any())
            await _userManager.RemoveFromRolesAsync(user, existingRoles);

        if (!string.IsNullOrWhiteSpace(dto.Role))
            await _userManager.AddToRoleAsync(user, dto.Role);

        return Ok(ApiResponse<bool>.Ok(true, "Updated."));
    }

    /// <summary>
    /// Este formulario asigna UN rol, y lo hace borrando todos los que hubiera. Así que "los mismos
    /// roles" es: los que tiene son exactamente el que se manda. Vaciar el campo también es un
    /// cambio si antes tenía alguno.
    /// </summary>
    private static bool MismoJuegoDeRoles(IList<string> actuales, string? pedido) =>
        string.IsNullOrWhiteSpace(pedido)
            ? actuales.Count == 0
            : actuales.Count == 1 && string.Equals(actuales[0], pedido, StringComparison.Ordinal);

    [HttpPut("{id}/status")]
    public async Task<IActionResult> ToggleStatus(string id, [FromBody] ToggleStatusRequest dto, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "User not found."));

        user.IsActive = dto.IsActive;

        // Solo al apagar. Reactivar no revoca: no retira ningún derecho, y de todas formas la
        // sesión que hubiera ya está revocada desde que se apagó.
        if (!dto.IsActive)
            user.RevokeLiveSessions();

        await _userManager.UpdateAsync(user);
        var message = dto.IsActive ? "User activated." : "User deactivated.";
        return Ok(ApiResponse<bool>.Ok(true, message));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "User not found."));

        // El DELETE de esta superficie es una baja lógica: apaga la cuenta, no borra la fila. Da
        // igual para lo que aquí importa —retira el derecho a entrar—, así que revoca como el
        // apagado de arriba.
        user.IsActive = false;
        user.RevokeLiveSessions();
        await _userManager.UpdateAsync(user);
        return Ok(ApiResponse<bool>.Ok(true, "Deactivated."));
    }

    private record SystemUserDto(string Id, string UserName, string Email, string Role,
        bool IsActive, DateTime? LastLoginAt, DateTime CreationDate);

    public record CreateSystemUserRequest(string Email, string Password, string Role, bool IsActive);
    public record UpdateSystemUserRequest(string Email, string Role, bool IsActive);
    public record ToggleStatusRequest(bool IsActive);
}
