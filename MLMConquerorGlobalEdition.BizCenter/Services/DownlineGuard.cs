using MLMConquerorGlobalEdition.Domain.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>
/// LA SEGUNDA MITAD DE LA COMPROBACIÓN DE PROPIEDAD, la que <see cref="CallerIdentity"/> no puede
/// contestar sola porque necesita mirar el árbol.
/// </summary>
/// <remarks>
/// POR QUÉ NO BASTA <see cref="CallerIdentity.CanActOnMember"/> AQUÍ, y por qué esto NO es una
/// segunda regla. La regla de propiedad —"o es tu cuenta o eres personal"— sigue siendo la de
/// siempre y se aplica tal cual: es la primera línea de cada método de abajo. Lo que pasa en el
/// centro de negocios es que hay lecturas cuyo sujeto legítimo NO es la cuenta del que llama sino
/// un descendiente suyo: el visualizador del árbol binario abre nodo a nodo hacia abajo
/// (<c>dual-tree/node/{id}</c>), el informe del bono del coche despliega la rama de cada
/// embajador de la red (<c>car-bonus/ambassadors/{id}/branch</c>). Cerrar eso con
/// <c>CanActOnMember</c> a secas devolvería 403 en la pantalla de todo el mundo.
///
/// Así que la pregunta no cambia de regla, cambia de SUJETO: sigue siendo "¿esto es tuyo?", solo
/// que "tuyo" incluye tu descendencia en el árbol que esa pantalla recorre. Lo que había antes no
/// era ni una cosa ni la otra: los manejadores no miraban el token en absoluto, así que cualquier
/// cuenta autenticada leía el nodo binario, los puntos de pierna y la rama de comisiones de
/// CUALQUIER miembro cambiando una cadena en la URL.
///
/// DOS ÁRBOLES, Y NO SON INTERCAMBIABLES. <c>DualTeamTree</c> es el binario —dónde estás colocado,
/// de donde salen los puntos de pierna— y <c>GenealogyTree</c> es el de patrocinio —a quién
/// inscribiste—. Cada ruta se cierra contra el árbol que ella misma recorre; usar el otro dejaría
/// pasar exactamente a quien la pantalla no enseña.
///
/// LA CONTENCIÓN SE MIRA, en el binario, POR LA CLAVE COMPACTA <c>BinaryPath</c> (Plan P: el descendiente cae en
/// el rango de su ancestro, <c>DualBinaryPath.Contains</c>, lo mismo que usa <c>DualSubtree</c>), y en el de
/// patrocinio por <c>Node</c> (Plan M). Ya no por el prefijo de <c>HierarchyPath</c>, que falla con rutas de más de
/// 4.000 caracteres. Solo sin claves (InMemory, pruebas de lógica) se sube por la cadena de padres.
///
/// FALLA CERRADO en todos los caminos: sin identidad de miembro, con un identificador vacío, o si
/// el que llama no está en el árbol, la respuesta es que NO.
/// </remarks>
public interface IDownlineGuard
{
    /// <summary>
    /// ¿Puede leer el nodo binario de <paramref name="memberId"/>? Suyo, personal, o dentro de su
    /// propio subárbol binario.
    /// </summary>
    Task<bool> PuedeVerNodoBinarioAsync(ClaimsPrincipal? user, string? memberId, CancellationToken ct);

    /// <summary>
    /// ¿Puede leer la rama de patrocinio de <paramref name="memberId"/>? Suya, personal, o dentro
    /// de su propia red de patrocinio.
    /// </summary>
    Task<bool> PuedeVerRamaDePatrocinioAsync(ClaimsPrincipal? user, string? memberId, CancellationToken ct);

    /// <summary>
    /// ¿Puede colocar o descolocar a <paramref name="memberId"/>? Solo si lo patrocina él mismo, o
    /// si es personal. La colocación mueve puntos de pierna, así que aquí NO vale con tenerlo en la
    /// red: tiene que ser un patrocinado directo, que es lo que la pantalla de colocaciones
    /// pendientes lista.
    /// </summary>
    Task<bool> PatrocinaAsync(ClaimsPrincipal? user, string? memberId, CancellationToken ct);

    /// <summary>
    /// ¿Puede colgar a alguien DEBAJO de <paramref name="targetParentMemberId"/>? Es el nodo
    /// destino, no el colocado: tiene que ser él mismo o alguien de su red de patrocinio, que es
    /// exactamente el conjunto que <c>GetAvailableNodes</c> ofrece en la pantalla.
    /// </summary>
    Task<bool> PuedeColocarBajoAsync(ClaimsPrincipal? user, string? targetParentMemberId, CancellationToken ct);
}

/// <inheritdoc cref="IDownlineGuard"/>
public sealed class DownlineGuard : IDownlineGuard
{
    private readonly AppDbContext _db;

    public DownlineGuard(AppDbContext db) => _db = db;

    public Task<bool> PuedeVerNodoBinarioAsync(ClaimsPrincipal? user, string? memberId, CancellationToken ct) =>
        SuyoOPersonalO(user, memberId, async (propio, objetivo) =>
        {
            // Plan P: por la clave compacta. El objetivo esta en mi red si su clave cae en el rango de la mia
            // (DualBinaryPath.Contains); antes se comparaba el prefijo de la ruta de texto.
            // El nodo mas reciente de cada uno (R8: no uno antiguo que quedara en mi red).
            var mio = await _db.DualTeamTree.AsNoTracking().Where(d => d.MemberId == propio)
                .OrderByDescending(d => d.CreationDate).ThenByDescending(d => d.Id).FirstOrDefaultAsync(ct);
            var suyo = await _db.DualTeamTree.AsNoTracking().Where(d => d.MemberId == objetivo)
                .OrderByDescending(d => d.CreationDate).ThenByDescending(d => d.Id).FirstOrDefaultAsync(ct);
            if (mio is null || suyo is null) return false;
            if (mio.BinaryPath is not null && suyo.BinaryPath is not null)
                return DualBinaryPath.Contains(mio.BinaryPath, suyo.BinaryPath);

            // Sin claves (InMemory, pruebas de logica): subir por ParentMemberId desde el objetivo.
            var vistos = new HashSet<string>();
            for (var actual = suyo; actual is not null && vistos.Add(actual.MemberId);)
            {
                if (actual.MemberId == mio.MemberId) return true;
                var padre = actual.ParentMemberId;
                actual = padre is null ? null : await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == padre, ct);
            }
            return false;
        });

    public Task<bool> PuedeVerRamaDePatrocinioAsync(ClaimsPrincipal? user, string? memberId, CancellationToken ct) =>
        SuyoOPersonalO(user, memberId, async (propio, objetivo) =>
        {
            // Plan M (revisión M6): por Node, como el resto de lecturas del árbol de patrocinio. El nodo
            // vigente del que mira (R8); sin Node no hay red que abrir (falla cerrado).
            var raiz = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(propio, ct);
            if (raiz is null) return false;

            // R8 también sobre el objetivo: cuenta su nodo vigente, no uno antiguo que quedara bajo el mío.
            var nodoObjetivo = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(objetivo, ct);
            return nodoObjetivo is not null && nodoObjetivo.IsDescendantOf(raiz);
        });

    public Task<bool> PatrocinaAsync(ClaimsPrincipal? user, string? memberId, CancellationToken ct) =>
        SuyoOPersonalO(user, memberId, async (propio, objetivo) =>
            await _db.MemberProfiles
                .AsNoTracking()
                .AnyAsync(m => m.MemberId == objetivo
                            && m.SponsorMemberId == propio
                            && !m.IsDeleted, ct));

    public Task<bool> PuedeColocarBajoAsync(ClaimsPrincipal? user, string? targetParentMemberId, CancellationToken ct) =>
        PuedeVerRamaDePatrocinioAsync(user, targetParentMemberId, ct);

    /// <summary>
    /// La regla de propiedad de siempre —<see cref="CallerIdentity.CanActOnMember"/>, o es tuya o
    /// eres personal— y, solo si dice que no, la pregunta de contención que corresponda. El orden
    /// importa: el personal no tiene <c>memberId</c>, así que preguntarle primero por el árbol lo
    /// dejaría fuera de todo.
    /// </summary>
    private static Task<bool> SuyoOPersonalO(
        ClaimsPrincipal? user,
        string? memberId,
        Func<string, string, Task<bool>> contencion)
    {
        if (string.IsNullOrWhiteSpace(memberId)) return Task.FromResult(false);
        if (user.CanActOnMember(memberId))       return Task.FromResult(true);

        var propio = CallerIdentity.MemberIdOf(user);
        if (string.IsNullOrWhiteSpace(propio))   return Task.FromResult(false);

        return contencion(propio, memberId);
    }
}
