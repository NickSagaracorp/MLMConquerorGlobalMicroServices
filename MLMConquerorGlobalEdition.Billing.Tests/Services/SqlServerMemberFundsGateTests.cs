using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// Un [Fact] que se marca como SALTADO (no como pasado) cuando no hay SQL Server alcanzable.
/// </summary>
/// <remarks>
/// xunit 2 no deja saltar desde dentro de la prueba; el paquete habitual para eso
/// (Xunit.SkippableFact) es MS-PL, no MIT. Poner Skip en el constructor del atributo lo resuelve al
/// descubrir las pruebas, sin dependencias, y el informe dice "skipped" en vez de un verde falso.
/// </remarks>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerTestDb.Reachable)
            Skip = "SQL Server no alcanzable (define MLM_TEST_SQLSERVER para usar otra instancia).";
    }
}

public static class SqlServerTestDb
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("MLM_TEST_SQLSERVER")
        ?? "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;Encrypt=True;Connect Timeout=3;";

    private static readonly Lazy<bool> LazyReachable = new(() =>
    {
        try
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
            return true;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            return false;
        }
    });

    public static bool Reachable => LazyReachable.Value;

    public static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);
}

/// <summary>
/// El candado real contra SQL Server. EF InMemory no tiene sp_getapplock, así que esto es lo único
/// que prueba que el SQL crudo (DECLARE/EXEC/SELECT por SqlQuery&lt;int&gt;) corre, que el candado
/// excluye de verdad y que un fallo o un tiempo agotado no dejan la transacción colgada del
/// contexto. No escribe datos: el candado vive en la sesión y se suelta con la transacción.
/// </summary>
public class SqlServerMemberFundsGateTests
{
    private static string NuevoMiembro() => $"test-{Guid.NewGuid():N}";

    [SqlServerFact]
    public async Task Toma_el_candado_dentro_de_una_transaccion()
    {
        await using var db = SqlServerTestDb.Db();
        await using var tx = await new SqlServerMemberFundsGate(db).EnterAsync(NuevoMiembro());

        tx.Should().NotBeNull();
        db.Database.CurrentTransaction.Should().BeSameAs(tx);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_mismo_miembro_espera_y_otro_miembro_no()
    {
        var member = NuevoMiembro();

        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        await using var dbC = SqlServerTestDb.Db();

        var txA = await new SqlServerMemberFundsGate(dbA).EnterAsync(member);

        // Otro miembro no se espera aunque A tenga el suyo.
        await using (var txC = await new SqlServerMemberFundsGate(dbC).EnterAsync($"{member}-otro")
                         .WaitAsync(TimeSpan.FromSeconds(5)))
            await txC.RollbackAsync();

        // El mismo miembro queda bloqueado mientras A no suelte.
        var enterB = new SqlServerMemberFundsGate(dbB).EnterAsync(member);
        await Task.Delay(1500);
        enterB.IsCompleted.Should().BeFalse("A todavía tiene el candado del miembro");

        await txA.CommitAsync();
        await txA.DisposeAsync();

        await using var txB = await enterB.WaitAsync(TimeSpan.FromSeconds(10));
        txB.Should().NotBeNull();
        await txB.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Agotar_el_tiempo_es_ocupado_y_suelta_la_transaccion()
    {
        var member = NuevoMiembro();
        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        await using var txA = await new SqlServerMemberFundsGate(dbA).EnterAsync(member);

        var act = () => new SqlServerMemberFundsGate(dbB, lockTimeoutMs: 200).EnterAsync(member);

        await act.Should().ThrowAsync<MemberFundsBusyException>();
        dbB.Database.CurrentTransaction.Should().BeNull("una puerta ocupada no deja la transacción abierta");

        // El contexto sigue sirviendo: se puede volver a intentar en cuanto A suelta.
        await txA.RollbackAsync();
        await using var txB = await new SqlServerMemberFundsGate(dbB, lockTimeoutMs: 2000).EnterAsync(member);
        txB.Should().NotBeNull();
        await txB.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Si_la_consulta_del_candado_falla_deshace_y_suelta_la_transaccion()
    {
        var member = NuevoMiembro();
        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        await using var txA = await new SqlServerMemberFundsGate(dbA).EnterAsync(member);

        // B espera el candado y se cancela mientras espera: la consulta lanza.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var act = () => new SqlServerMemberFundsGate(dbB).EnterAsync(member, cts.Token);

        await act.Should().ThrowAsync<Exception>();
        dbB.Database.CurrentTransaction.Should().BeNull("la transacción se deshace aunque el token ya esté cancelado");
        await txA.RollbackAsync();
    }

    [SqlServerFact]
    public async Task En_un_lote_toma_varios_candados_y_salta_el_ocupado()
    {
        var libre   = NuevoMiembro();
        var ocupado = NuevoMiembro();
        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        await using var txA = await new SqlServerMemberFundsGate(dbA).EnterAsync(ocupado);

        var gate = new SqlServerMemberFundsGate(dbB);
        await using var tx = await dbB.Database.BeginTransactionAsync();

        (await gate.TryAcquireAsync(libre, TimeSpan.FromSeconds(2))).Should().BeTrue();
        (await gate.TryAcquireAsync(ocupado, TimeSpan.FromMilliseconds(200))).Should().BeFalse();
        dbB.Database.CurrentTransaction.Should().BeSameAs(tx, "un candado no obtenido no aborta el lote");

        await tx.RollbackAsync();
        await txA.RollbackAsync();
    }
}
