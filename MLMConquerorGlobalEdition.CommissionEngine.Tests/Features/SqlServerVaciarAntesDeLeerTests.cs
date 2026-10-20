using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

/// <summary>Plan Q (DQ1): lo que se paga se calcula con la cola de piernas vacia.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerVaciarAntesDeLeerTests
{
    /// <summary>El reloj del motor de comisiones (CommissionEngine.Services.IDateTimeProvider).</summary>
    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>El usuario del motor de comisiones: el manejador lo pide en el constructor y no lo usa antes de vaciar.</summary>
    private sealed class SinUsuario : ICurrentUserService
    {
        public string UserId => "plan-q-prueba";
        public string MemberId => string.Empty;
        public bool IsAdmin => true;
    }

    [SqlServerFact]
    public async Task El_residual_diario_vacia_la_cola_antes_de_calcular()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        // Revision del bloque 2 (M-2): un arranque con nodo de verdad, para probar que las piernas quedan bien (un delta de
        // pierna sin nodo ya no se descarta en silencio: se aparta a DualLegDeltaDeadLetters).
        var x = SqlServerReportes.NuevoId("PQV");
        var raiz = await DualTeamNodes.RootAsync(db, $"{x}P", DateTime.Now, "plan-q-prueba");
        db.DualTeamTree.Add(raiz);
        db.DualTeamTree.Add(await DualTeamNodes.ChildAsync(db, raiz, $"{x}L", TreeSide.Left, DateTime.Now, "plan-q-prueba"));
        var arranque = $"{x}L";
        db.DualLegDeltas.Add(new DualLegDelta
        {
            StartMemberId = arranque, Side = TreeSide.Left, Points = 9, NodeCount = 1,
            CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba"
        });
        await db.SaveChangesAsync();
        // Dentro de la transaccion, que se deshace: sin tipos activos el manejador termina justo despues de vaciar.
        await db.Database.ExecuteSqlRawAsync("UPDATE CommissionTypes SET IsActive = 0 WHERE IsActive = 1");

        var r = await new CalculateDailyResidualHandler(db, new Reloj(), new SinUsuario())
            .Handle(new CalculateDailyResidualCommand(new DateTime(2000, 1, 1)), CancellationToken.None);

        r.ErrorCode.Should().Be("NO_RESIDUAL_TYPES");
        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == arranque))
            .Should().Be(0, "el manejador vacia la cola de piernas antes de leer DualTeamPoints");
        var l = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == arranque);
        var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
        (l.LeftLegPoints, l.LeftLegCount).Should().Be((9m, 1), "la pierna del arranque");
        (p.LeftLegPoints, p.LeftLegCount).Should().Be((9m, 1), "y la de su ascendiente");
        await tx.RollbackAsync();
    }
}
