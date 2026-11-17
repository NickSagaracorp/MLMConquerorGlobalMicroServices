using Microsoft.Data.SqlClient;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Reconoce el error de clave duplicada de SQL Server (2601 índice único, 2627 restricción) en cualquier nivel de la excepción.</summary>
public static class DuplicateKeyErrors
{
    public static bool IsDuplicateKey(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is SqlException { Number: 2601 or 2627 }) return true;
        return false;
    }
}

/// <summary>
/// Reconoce que SQL Server eligió esta escritura como víctima de un interbloqueo (1205) en cualquier nivel de la
/// excepción. Dos escrituras concurrentes del mismo día (el job nocturno y la foto manual) pueden interbloquearse al
/// insertar en el mismo rango del índice único; la víctima no escribió nada y puede repetirse entera.
/// </summary>
public static class DeadlockErrors
{
    public static bool IsDeadlock(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is SqlException { Number: 1205 }) return true;
        return false;
    }
}

/// <summary>
/// Repite una escritura de reportes que chocó con otra igual a la vez (el job nocturno y la carga manual): clave
/// duplicada o víctima de un interbloqueo. En los dos casos esta escritura no dejó nada y puede repetirse entera; la
/// repetición ve lo que escribió la otra.
/// </summary>
public static class WriteConflictRetry
{
    public const int MaxAttempts = 3;

    public static bool IsConflict(Exception ex) => DuplicateKeyErrors.IsDuplicateKey(ex) || DeadlockErrors.IsDeadlock(ex);

    /// <param name="beforeRetry">Lo que hay que limpiar antes de repetir (normalmente el ChangeTracker).</param>
    public static async Task<T> RunAsync<T>(Func<Task<T>> write, Action? beforeRetry = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await write();
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsConflict(ex))
            {
                beforeRetry?.Invoke();
            }
        }
    }

    public static Task RunAsync(Func<Task> write, Action? beforeRetry = null) =>
        RunAsync(async () => { await write(); return true; }, beforeRetry);
}
