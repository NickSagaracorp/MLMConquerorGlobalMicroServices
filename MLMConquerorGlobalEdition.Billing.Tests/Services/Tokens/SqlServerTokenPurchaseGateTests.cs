using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// El candado real contra SQL Server. EF InMemory no tiene sp_getapplock, así que esto es lo único
/// que prueba que el SQL crudo (DECLARE/EXEC/SELECT por SqlQuery&lt;int&gt;) corre y que el candado
/// excluye de verdad. No escribe datos: el candado vive en la sesión y se suelta con la transacción.
///
/// Si no hay SQL Server alcanzable (CI, otra máquina) cada prueba vuelve sin afirmar nada: xunit 2
/// no permite saltar en tiempo de ejecución y no hay convención propia para pruebas de base real.
/// La cadena se puede cambiar con MLM_TEST_SQLSERVER.
/// </summary>
public class SqlServerTokenPurchaseGateTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("MLM_TEST_SQLSERVER")
        ?? "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;Encrypt=True;Connect Timeout=3;";

    private static readonly Lazy<bool> Reachable = new(() =>
    {
        try
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    });

    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    [Fact]
    public async Task Con_SQL_Server_disponible_toma_el_candado_dentro_de_una_transaccion()
    {
        if (!Reachable.Value) return;

        await using var db = Db();
        await using var tx = await new SqlServerTokenPurchaseGate(db).EnterAsync($"test-{Guid.NewGuid():N}");

        tx.Should().NotBeNull();
        db.Database.CurrentTransaction.Should().BeSameAs(tx);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Con_SQL_Server_disponible_el_mismo_miembro_espera_y_otro_miembro_no()
    {
        if (!Reachable.Value) return;

        var member = $"test-{Guid.NewGuid():N}";

        await using var dbA = Db();
        await using var dbB = Db();
        await using var dbC = Db();

        var txA = await new SqlServerTokenPurchaseGate(dbA).EnterAsync(member);

        // Otro miembro no se espera aunque A tenga el suyo.
        await using (var txC = await new SqlServerTokenPurchaseGate(dbC).EnterAsync($"{member}-otro")
                         .WaitAsync(TimeSpan.FromSeconds(5)))
            await txC.RollbackAsync();

        // El mismo miembro queda bloqueado mientras A no suelte.
        var enterB = new SqlServerTokenPurchaseGate(dbB).EnterAsync(member);
        await Task.Delay(1500);
        enterB.IsCompleted.Should().BeFalse("A todavía tiene el candado del miembro");

        await txA.CommitAsync();
        await txA.DisposeAsync();

        await using var txB = await enterB.WaitAsync(TimeSpan.FromSeconds(10));
        txB.Should().NotBeNull();
        await txB.RollbackAsync();
    }
}
