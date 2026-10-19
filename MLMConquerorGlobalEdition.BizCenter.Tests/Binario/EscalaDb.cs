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
    public static async Task<TimeSpan> MedianaAsync(ITestOutputHelper salida, string que, Func<int, Task> paso)
    {
        var tiempos = new List<TimeSpan>();
        for (var i = 0; i < 3; i++)
        {
            var c = Stopwatch.StartNew();
            await paso(i);
            c.Stop();
            tiempos.Add(c.Elapsed);
        }
        var mediana = tiempos.OrderBy(t => t).ElementAt(1);
        salida.WriteLine($"[ESCALA] {que}: {string.Join(" / ", tiempos.Select(t => $"{t.TotalMilliseconds:0} ms"))} -> mediana {mediana.TotalMilliseconds:0} ms");
        return mediana;
    }
}
