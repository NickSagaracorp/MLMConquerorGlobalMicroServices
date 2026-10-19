using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Jobs;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Jobs;

/// <summary>Plan Q (Q-R1): la evaluacion de rango lee las piernas del binario; la cola de rango las vacia antes.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColaDeRangoVaciaPiernasTests
{
    [SqlServerFact]
    public async Task La_cola_de_rango_vacia_las_piernas_antes_de_evaluar()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var x = SqlServerReportes.NuevoId("PQK");
        db.DualLegDeltas.Add(new DualLegDelta
        {
            StartMemberId = x, Side = TreeSide.Left, Points = 9, NodeCount = 1,
            CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba"
        });
        db.RankEvaluationQueue.Add(new RankEvaluationQueue
        {
            TriggerMemberId = x, EvaluateMemberId = x, TriggerEvent = RankEvaluationTrigger.Enrollment,
            TriggerDate = new DateTime(2000, 1, 1), CreatedBy = "plan-q-prueba", CreationDate = DateTime.Now
        });
        await db.SaveChangesAsync();
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<EvaluateRankCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("plan-q-prueba: sin evaluar"));

        await new ProcessRankQueueJob(mediador.Object, db, NullLogger<ProcessRankQueueJob>.Instance).ExecuteAsync();

        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == x)).Should().Be(0);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Revision del bloque 2 (I-4): el barrido nocturno evalua a todos los embajadores con las piernas; vacia la cola entera
    /// antes de la fase 1 (sin tope de tandas, con la misma espera de candado que las comisiones).
    /// </summary>
    [SqlServerFact]
    public async Task El_barrido_nocturno_vacia_las_piernas_antes_de_evaluar()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var x = SqlServerReportes.NuevoId("PQS");
        var raiz = await DualTeamNodes.RootAsync(db, $"{x}P", DateTime.Now, "plan-q-prueba");
        db.DualTeamTree.Add(raiz);
        db.DualTeamTree.Add(await DualTeamNodes.ChildAsync(db, raiz, $"{x}L", TreeSide.Left, DateTime.Now, "plan-q-prueba"));
        db.DualLegDeltas.Add(new DualLegDelta
        {
            StartMemberId = $"{x}P", Side = TreeSide.Left, Points = 0, NodeCount = 1,
            CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba"
        });
        db.DualLegDeltas.Add(new DualLegDelta
        {
            StartMemberId = $"{x}L", Side = TreeSide.Left, Points = 9, NodeCount = 1,
            CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba"
        });
        await db.SaveChangesAsync();
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<EvaluateRankCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<RankEvaluationResponse>.Failure("PLAN_Q_PRUEBA", "sin evaluar")));

        await new RankEvaluationSweepJob(mediador.Object, db, NullLogger<RankEvaluationSweepJob>.Instance).ExecuteAsync();

        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}L")).Should().Be(0);
        var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
        (p.LeftLegCount, p.LeftLegPoints).Should().Be((2, 9m));
        await tx.RollbackAsync();
    }
}
