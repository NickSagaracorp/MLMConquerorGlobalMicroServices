using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Quién llama, leído del token de la petición.
/// </summary>
/// <remarks>
/// EL NOMBRE DEL CLAIM ESTABA MAL. <c>MemberId</c> leía <c>member_id</c> y el emisor escribe
/// <c>memberId</c>, así que esta propiedad devolvía SIEMPRE cadena vacía. Hoy no lo nota nadie
/// —ningún manejador de SignupAPI la usa; <c>ChangePasswordHandler</c> hasta lo documenta como el
/// motivo de sacar el identificador de otro sitio— y por eso es una trampa y no un fallo: la primera
/// comprobación de propiedad que alguien construya sobre esto compararía contra vacío y no
/// protegería nada. Los dos nombres se resuelven ahora en <see cref="CallerIdentity"/>, que es el
/// mismo sitio del que tiran los controladores.
///
/// <c>UserId</c> acepta además <c>sub</c>, como ya hacían a mano los trece endpoints de
/// <c>AuthController</c>: cuál de los dos llega depende del mapeo de claims entrantes.
/// </remarks>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string UserId => CallerIdentity.UserIdOf(User) ?? string.Empty;
    public string MemberId => CallerIdentity.MemberIdOf(User) ?? string.Empty;
    public string Email => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public bool IsAdmin => CallerIdentity.IsStaff(User);
    public IEnumerable<string> Roles => User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
}
