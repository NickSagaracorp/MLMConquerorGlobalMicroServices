using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Bloque D: un [Fact] que solo corre contra la base de escala. Se salta si no esta definida
/// MLM_ESCALA_SQLSERVER y se NIEGA (prueba saltada con el motivo) si la cadena apunta a cualquier base que no sea
/// exactamente MLMConquerorGlobalEditionDb_Escala, o si la base a la que conecta no se llama asi.
/// </summary>
public sealed class EscalaFactAttribute : FactAttribute
{
    public EscalaFactAttribute()
    {
        if (EscalaDb.Motivo is { } motivo) Skip = motivo;
    }
}

/// <summary>Plan Q (Tarea 33): el [Theory] de <see cref="EscalaFactAttribute"/>, con los mismos cerrojos.</summary>
public sealed class EscalaTheoryAttribute : TheoryAttribute
{
    public EscalaTheoryAttribute()
    {
        if (EscalaDb.Motivo is { } motivo) Skip = motivo;
    }
}

public static class EscalaDb
{
    public const string Base = "MLMConquerorGlobalEditionDb_Escala";
    public const string Variable = "MLM_ESCALA_SQLSERVER";

    public static readonly string? ConnectionString = Environment.GetEnvironmentVariable(Variable);

    private static readonly Lazy<string?> LazyMotivo = new(() =>
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return $"Sin {Variable}: las mediciones de escala solo corren contra {Base}.";
        string? declarada;
        try { declarada = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog; }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
        {
            return $"{Variable} no es una cadena de conexion valida.";
        }
        if (!string.Equals(declarada, Base, StringComparison.Ordinal))
            return $"{Variable} apunta a '{declarada}': las mediciones de escala solo corren contra {Base}.";
        try
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
            using var cmd = new SqlCommand("SELECT DB_NAME()", c);
            var real = (string)cmd.ExecuteScalar()!;
            return real == Base ? null : $"La conexion abre '{real}', no {Base}.";
        }
        catch (SqlException ex)
        {
            return $"No se pudo abrir {Base}: {ex.Message}";
        }
    });

    /// <summary>Null si se puede medir; si no, el motivo por el que la prueba se salta.</summary>
    public static string? Motivo => LazyMotivo.Value;

    public static AppDbContext Db(int timeoutSegundos = 600)
    {
        if (Motivo is { } m) throw new InvalidOperationException(m);
        // El mismo tiempo en la cadena: SqlTransaction.Rollback no usa el de EF, sino el de la conexion.
        var cadena = new SqlConnectionStringBuilder(ConnectionString) { CommandTimeout = timeoutSegundos }.ConnectionString;
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cadena, o => o.CommandTimeout(timeoutSegundos)).Options);
    }

    /// <summary>
    /// Ejecuta <paramref name="paso"/> tres veces (la primera calienta la cache) e imprime los tres tiempos y la
    /// mediana, que es lo que se compara con el objetivo.
    /// </summary>
    /// <remarks>
    /// Plan Q (Tarea 33): con <paramref name="db"/>, tambien las lecturas logicas y la CPU de la sesion de cada ejecucion
    /// (sys.dm_exec_sessions, con la conexion abierta durante las tres) y la carga de la maquina antes de medir: con la maquina
    /// cargada el tiempo varia mucho y las lecturas y la CPU no.
    /// </remarks>
    public static async Task<TimeSpan> MedianaAsync(ITestOutputHelper salida, string que, Func<int, Task> paso, AppDbContext? db = null)
    {
        var tiempos = new List<TimeSpan>();
        var costes = new List<(long Lecturas, long Cpu)>();
        var abrir = db is not null && db.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (abrir) await db!.Database.OpenConnectionAsync();
        try
        {
            var carga = db is null ? null : await CargaAsync(db);
            for (var i = 0; i < 3; i++)
            {
                var antes = db is null ? default : await SesionAsync(db);
                var c = Stopwatch.StartNew();
                await paso(i);
                c.Stop();
                tiempos.Add(c.Elapsed);
                if (db is not null)
                {
                    var despues = await SesionAsync(db);
                    costes.Add((despues.Lecturas - antes.Lecturas, despues.Cpu - antes.Cpu));
                }
            }
            var mediana = tiempos.OrderBy(t => t).ElementAt(1);
            var coste = costes.Count == 0 ? "" :
                $"; lecturas {string.Join(" / ", costes.Select(k => k.Lecturas))}, CPU {string.Join(" / ", costes.Select(k => $"{k.Cpu} ms"))}; {carga}";
            salida.WriteLine($"[ESCALA] {que}: {string.Join(" / ", tiempos.Select(t => $"{t.TotalMilliseconds:0} ms"))} -> mediana {mediana.TotalMilliseconds:0} ms{coste}");
            return mediana;
        }
        finally
        {
            if (abrir) await db!.Database.CloseConnectionAsync();
        }
    }

    public sealed record Sesion(long Lecturas, long Cpu);

    /// <summary>Lecturas logicas y CPU (ms) acumuladas de la sesion de <paramref name="db"/> (la conexion tiene que estar abierta).</summary>
    public static async Task<Sesion> SesionAsync(AppDbContext db) =>
        (await db.Database.SqlQueryRaw<Sesion>(
                "SELECT CAST(logical_reads AS bigint) AS Lecturas, CAST(cpu_time AS bigint) AS Cpu FROM sys.dm_exec_sessions WHERE session_id = @@SPID")
            .ToListAsync())[0];

    private sealed record Monitor(int Sistema, int Sql);

    /// <summary>
    /// La carga de la maquina en el ultimo minuto segun SQL Server (RING_BUFFER_SCHEDULER_MONITOR: CPU total ocupada y la de
    /// SQL Server). Texto para la salida; si no se puede leer, lo dice.
    /// </summary>
    public static async Task<string> CargaAsync(AppDbContext db)
    {
        try
        {
            var m = (await db.Database.SqlQueryRaw<Monitor>("""
                SELECT TOP 1 100 - r.value('(./Record/SchedulerMonitorEvent/SystemHealth/SystemIdle)[1]', 'int') AS Sistema,
                       r.value('(./Record/SchedulerMonitorEvent/SystemHealth/ProcessUtilization)[1]', 'int') AS Sql
                FROM (SELECT CAST(record AS xml) AS r, [timestamp] FROM sys.dm_os_ring_buffers
                      WHERE ring_buffer_type = N'RING_BUFFER_SCHEDULER_MONITOR' AND record LIKE N'%<SystemHealth>%') x
                ORDER BY x.[timestamp] DESC
                """).ToListAsync()).FirstOrDefault();
            return m is null ? "carga desconocida" : $"CPU de la maquina antes de medir {m.Sistema} % (SQL Server {m.Sql} %)";
        }
        catch (SqlException)
        {
            return "carga desconocida";
        }
    }
}
