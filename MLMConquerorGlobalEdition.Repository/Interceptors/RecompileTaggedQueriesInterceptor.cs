using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.Repository.Interceptors;

/// <summary>
/// Plan M: añade OPTION (RECOMPILE) a las consultas que llevan <see cref="GenealogySubtree.RecompileTag"/>
/// (todas las que pasan por GenealogySubtree.Under) y <see cref="DualSubtree.RecompileTag"/> (el rango de la
/// clave compacta del binario, Plan P). El mismo SQL del equipo sirve a un equipo de 30
/// personas y a uno de 119.400: con el plan en caché del pequeño (bucles anidados), el grande tardaba
/// 3–6 s; compilado para su rango, 73 ms. Recompilar cuesta milisegundos; un plan equivocado, segundos.
/// Va en AppDbContext.OnConfiguring, como los demás interceptores, para que ningún anfitrión lo olvide.
/// </summary>
public sealed class RecompileTaggedQueriesInterceptor : DbCommandInterceptor
{
    private const string Hint = "OPTION (RECOMPILE)";

    /// <summary>El texto del comando con la pista al final si lleva la etiqueta y aún no la tiene.</summary>
    public static string Apply(string commandText) =>
        (commandText.Contains(GenealogySubtree.RecompileTag, StringComparison.Ordinal)
         || commandText.Contains(DualSubtree.RecompileTag, StringComparison.Ordinal))
        && !commandText.TrimEnd().EndsWith(Hint, StringComparison.Ordinal)
            ? commandText.TrimEnd().TrimEnd(';') + "\n" + Hint
            : commandText;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        command.CommandText = Apply(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        command.CommandText = Apply(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        command.CommandText = Apply(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        command.CommandText = Apply(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
