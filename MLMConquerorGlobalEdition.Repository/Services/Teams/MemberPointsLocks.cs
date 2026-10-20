using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Plan Q (tercera revision del bloque 2, I-a): serializa por miembro los guardados que cambian sus puntos de membresia
/// (DQ16). <see cref="MembershipPointsDeltas"/> calcula el cambio neto ANTES de que EF abra su transaccion; sin candado, dos
/// guardados del mismo miembro leian lo mismo (dos activaciones se veian inactivas, dos cancelaciones restaban dos veces).
/// <para>
/// Un sp_getapplock exclusivo por miembro, en orden ordinal (sin interbloqueos entre guardados), y despues lecturas frescas.
/// Sin transaccion de quien llama, el candado es de SESION: se toma en SavingChanges y se suelta en SavedChanges (tras la
/// confirmacion de EF) o en SaveChangesFailed/Canceled, con la conexion abierta entre medias; asi el siguiente lee lo ya
/// confirmado. Con transaccion de quien llama, es de TRANSACCION: se suelta al confirmarla o deshacerla (soltarlo antes
/// dejaria leer al siguiente lo que aun no se confirmo). Se eligio frente a un rowversion en MemberStatistics con
/// reintento porque el calculo lee varias tablas (suscripciones, estadisticas, colas) y reintentarlo desde un
/// interceptor obligaria a rehacer el guardado entero de quien llama; el candado no cambia el esquema ni los llamadores.
/// </para>
/// Solo en proveedores relacionales; en InMemory no hace nada.
/// </summary>
public static class MemberPointsLocks
{
    /// <summary>Espera maxima por candado (por debajo de los 30 s del comando de EF).</summary>
    public const int LockTimeoutMs = 20_000;

    private sealed class Tomados
    {
        public readonly List<string> Recursos = new();
    }

    private static readonly ConditionalWeakTable<DbContext, Tomados> PorContexto = new();

    public static string Resource(string memberId) => "member-points:" + memberId;

    /// <summary>Toma los candados de los miembros (los que no tenga ya este contexto), en orden ordinal.</summary>
    public static async Task AcquireAsync(DbContext context, IEnumerable<string?> memberIds, CancellationToken ct = default)
    {
        if (!context.Database.IsRelational()) return;
        var enTransaccion = context.Database.CurrentTransaction is not null;
        var tomados = PorContexto.GetOrCreateValue(context);
        var recursos = memberIds.Where(m => !string.IsNullOrEmpty(m)).Select(m => Resource(m!))
            .Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal)
            .Where(r => enTransaccion || !tomados.Recursos.Contains(r, StringComparer.Ordinal))
            .ToList();
        if (recursos.Count == 0) return;

        if (!enTransaccion && tomados.Recursos.Count == 0)
            await context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var r in recursos)
            {
                var dueno = enTransaccion ? "Transaction" : "Session";
                var rc = (await context.Database.SqlQueryRaw<int>(
                        "DECLARE @rc int; EXEC @rc = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = {1}, @LockTimeout = {2}; SELECT @rc AS [Value];",
                        r, dueno, LockTimeoutMs)
                    .ToListAsync(ct).ConfigureAwait(false)).Single();
                if (rc < 0)
                    throw new InvalidOperationException(
                        $"No se obtuvo el candado de puntos {r} (sp_getapplock devolvio {rc}); el guardado no se hizo.");
                if (!enTransaccion) tomados.Recursos.Add(r);
            }
        }
        catch
        {
            await ReleaseAsync(context).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Suelta los candados de sesion de este contexto y cierra la conexion que abrio. Sin candados, nada.</summary>
    public static async Task ReleaseAsync(DbContext context)
    {
        if (!PorContexto.TryGetValue(context, out var tomados) || tomados.Recursos.Count == 0) return;
        var recursos = tomados.Recursos.ToList();
        tomados.Recursos.Clear();
        try
        {
            foreach (var r in recursos)
                await context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_releaseapplock @Resource = {0}, @LockOwner = 'Session';", r).ConfigureAwait(false);
        }
        catch
        {
            // La conexion cayo: SQL Server ya los solto con la sesion.
        }
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Como <see cref="ReleaseAsync"/>, para el SaveChanges sincrono.</summary>
    public static void Release(DbContext context)
    {
        if (!PorContexto.TryGetValue(context, out var tomados) || tomados.Recursos.Count == 0) return;
        var recursos = tomados.Recursos.ToList();
        tomados.Recursos.Clear();
        try
        {
            foreach (var r in recursos)
                context.Database.ExecuteSqlRaw("EXEC sp_releaseapplock @Resource = {0}, @LockOwner = 'Session';", r);
        }
        catch
        {
            // La conexion cayo: SQL Server ya los solto con la sesion.
        }
        finally
        {
            context.Database.CloseConnection();
        }
    }
}
