using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Tarea 2: el candado del binario. Cada prueba usa un padre inventado (no escribe filas): los
/// candados de aplicacion son de la base, no de una tabla, y se sueltan al deshacer la transaccion.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDualTeamGateTests
{
    [SqlServerFact]
    public async Task El_mismo_hueco_espera_y_otro_hueco_no()
    {
        var p = BinarioDb.Prefijo("PPG");
        await using var a = SqlServerReportes.Db();
        await using var b = SqlServerReportes.Db();
        await using var txA = await a.Database.BeginTransactionAsync();
        await DualTeamGate.EnterSlotAsync(a, p, TreeSide.Left);

        await using (var txB = await b.Database.BeginTransactionAsync())
        {
            var mismo = () => DualTeamGate.EnterSlotAsync(b, p, TreeSide.Left, lockTimeoutMs: 500);
            await mismo.Should().ThrowAsync<DualTeamBusyException>();
            await txB.RollbackAsync();
        }

        await using (var txB = await b.Database.BeginTransactionAsync())
        {
            await DualTeamGate.EnterSlotAsync(b, p, TreeSide.Right, lockTimeoutMs: 500); // otro hueco: no espera
            await txB.RollbackAsync();
        }
        await txA.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_movimiento_espera_a_las_colocaciones_y_las_detiene()
    {
        var p = BinarioDb.Prefijo("PPG");
        await using var a = SqlServerReportes.Db();
        await using var b = SqlServerReportes.Db();

        await using (var txA = await a.Database.BeginTransactionAsync())
        {
            await DualTeamGate.EnterSlotAsync(a, p, TreeSide.Left);
            await using var txB = await b.Database.BeginTransactionAsync();
            var mover = () => DualTeamGate.EnterTreeExclusiveAsync(b, lockTimeoutMs: 500);
            await mover.Should().ThrowAsync<DualTeamBusyException>("una colocacion tiene el arbol en compartido");
            await txB.RollbackAsync();
            await txA.RollbackAsync();
        }

        await using (var txB = await b.Database.BeginTransactionAsync())
        {
            await DualTeamGate.EnterTreeExclusiveAsync(b, lockTimeoutMs: 2000); // ya no hay colocaciones
            await using var txA = await a.Database.BeginTransactionAsync();
            var colocar = () => DualTeamGate.EnterSlotAsync(a, p, TreeSide.Left, lockTimeoutMs: 500);
            await colocar.Should().ThrowAsync<DualTeamBusyException>("el movimiento tiene el arbol en exclusivo");
            await txA.RollbackAsync();
            await txB.RollbackAsync();
        }
    }

    [SqlServerFact]
    public async Task Sin_transaccion_falla_en_vez_de_tomar_un_candado_que_no_protege_nada()
    {
        await using var db = SqlServerReportes.Db();
        var act = () => DualTeamGate.EnterSlotAsync(db, "PPG-SIN-TX", TreeSide.Left);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
