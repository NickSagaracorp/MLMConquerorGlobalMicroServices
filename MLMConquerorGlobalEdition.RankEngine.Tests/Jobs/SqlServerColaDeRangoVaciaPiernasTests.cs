using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Jobs;

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
}
