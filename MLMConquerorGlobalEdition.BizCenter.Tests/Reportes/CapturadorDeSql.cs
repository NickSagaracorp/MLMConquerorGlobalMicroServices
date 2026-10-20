using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q: guarda el texto y los valores de parametros de cada comando que EF manda.</summary>
public sealed class CapturadorDeSql : DbCommandInterceptor
{
    public readonly List<string> Textos = new();
    public readonly List<string> Valores = new();

    /// <summary>Un contexto contra la base local con este capturador.</summary>
    public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(SqlServerReportes.ConnectionString).AddInterceptors(this).Options);

    public void Limpiar()
    {
        Textos.Clear();
        Valores.Clear();
    }

    private void Guardar(DbCommand c)
    {
        Textos.Add(c.CommandText);
        foreach (DbParameter p in c.Parameters) Valores.Add(Convert.ToString(p.Value) ?? string.Empty);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
    {
        Guardar(c);
        return ValueTask.FromResult(r);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default)
    {
        Guardar(c);
        return ValueTask.FromResult(r);
    }
}
