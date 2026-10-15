using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services;

/// <summary>
/// Serializa todo lo que mueve el dinero de UN miembro desde Billing: comprar tokens (con comisiones
/// o eWallet) y reservar sus comisiones para un pago. Lo de miembros distintos no se espera.
/// </summary>
/// <remarks>
/// Sin un candado compartido, un pago (PayoutOrchestrator o el lote CSV) podía leer las comisiones
/// pendientes, la compra calcular el saldo gastable con esas mismas comisiones aún sin reservar y
/// debitar, y el pago reservarlas después: el miembro cobraba dos veces el mismo dinero.
/// </remarks>
public interface IMemberFundsGate
{
    /// <summary>
    /// Abre una transacción y toma dentro el candado exclusivo del miembro. Se suelta al confirmar o
    /// deshacer. Para operaciones de un solo miembro.
    /// </summary>
    /// <exception cref="MemberFundsBusyException">Si no se obtuvo el candado a tiempo.</exception>
    Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Toma el candado del miembro dentro de la transacción YA abierta (lotes de varios miembros) y
    /// devuelve false si no lo obtuvo a tiempo; la transacción sigue viva. Quien toma varios debe
    /// pedirlos en orden ordinal de memberId para que dos lotes no se interbloqueen.
    /// </summary>
    Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default);
}

public sealed class MemberFundsBusyException(string memberId)
    : Exception($"Another operation on the funds of member {memberId} is in progress.");

/// <summary>El nombre del candado. Uno solo para compras y pagos: si difiriera, no se excluirían.</summary>
public static class MemberFundsLock
{
    public static string Resource(string memberId) => $"member-funds:{memberId}";
}

/// <summary>
/// <c>sp_getapplock</c> con dueño Transaction: vive en SQL Server, así que vale con varias
/// instancias de Billing y con AdminAPI (que ejecuta pagos con el mismo orquestador). Preferido a
/// SERIALIZABLE (resolvería la carrera con un interbloqueo) y a un contador con RowVersion
/// (TokenBalance no tiene ni índice único ni RowVersion real).
/// </summary>
public sealed class SqlServerMemberFundsGate : IMemberFundsGate
{
    public const int DefaultLockTimeoutMs = 15000;

    private readonly AppDbContext _db;
    private readonly int          _lockTimeoutMs;

    /// <param name="lockTimeoutMs">
    /// Cuánto esperar al candado. Configurable para probar el camino de "ocupado" sin esperar 15 s.
    /// </param>
    public SqlServerMemberFundsGate(AppDbContext db, int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        _db            = db;
        _lockTimeoutMs = lockTimeoutMs;
    }

    public async Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);

        int code;
        try
        {
            code = await GetAppLockAsync(memberId, _lockTimeoutMs, ct);
        }
        catch
        {
            // Si la consulta del candado falla (red, cancelación) la transacción no puede quedar
            // abierta en el contexto: el siguiente BeginTransaction fallaría. Se deshace sin el token
            // de la petición, que puede ser justo el que se canceló.
            await ReleaseAsync(tx);
            throw;
        }

        if (code >= 0)
            return tx;

        await ReleaseAsync(tx);
        throw new MemberFundsBusyException(memberId);
    }

    public async Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("TryAcquireAsync needs an open transaction: the lock is owned by it.");

        return await GetAppLockAsync(memberId, (int)timeout.TotalMilliseconds, ct) >= 0;
    }

    /// <summary>
    /// 0 o 1 = concedido; negativo = tiempo agotado (-1), cancelado (-2), interbloqueo (-3) o error (-999).
    /// </summary>
    private async Task<int> GetAppLockAsync(string memberId, int timeoutMs, CancellationToken ct)
    {
        var resource = MemberFundsLock.Resource(memberId);

        // ToListAsync sin componer: EF manda el SQL tal cual. Un FirstAsync lo envolvería en una
        // subconsulta y SQL Server rechazaría el DECLARE/EXEC.
        var result = await _db.Database
            .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {timeoutMs}; SELECT @r AS [Value]")
            .ToListAsync(ct);

        return result.Count == 1 ? result[0] : -999;
    }

    private static async Task ReleaseAsync(IDbContextTransaction tx)
    {
        try
        {
            await tx.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Si la conexión ya cayó, deshacer también falla y SQL Server descarta la transacción
            // por su cuenta. Lo que importa es soltarla del contexto y no tapar el error original.
        }
        finally
        {
            await tx.DisposeAsync();
        }
    }
}
