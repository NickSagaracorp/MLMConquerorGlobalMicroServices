using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Jobs;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan Q (Bloque 4): una fila SponsorLine por alta; el trabajo la expande por la linea en SQL.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDeltasPorLineaTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [SqlServerFact]
    public async Task Un_delta_de_linea_llega_a_todos_los_ascendientes_y_encola_rango_a_10_niveles()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        // Los pendientes de la base local no son de esta prueba: se dan por aplicados dentro de la transaccion.
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var ids = Enumerable.Range(0, 12).Select(_ => SqlServerReportes.NuevoId("DL")).ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            ReportesDb.Miembro(db, ids[i], i == 0 ? null : ids[i - 1]);
            ReportesDb.Nodo(db, ids[i], i == 0 ? null : ids[i - 1], ReportesDb.Ruta(ids[..(i + 1)]), i + 1);
            ReportesDb.Puntos(db, ids[i], 100);
        }
        var delta = new MemberStatisticDelta
        {
            MemberId = ids[11], Scope = MemberStatisticDeltaScope.SponsorLine, EnrollmentPointsDelta = 6,
            EnrollmentTeamSizeDelta = 1, QualifiedSponsoredMembersDelta = 1, SourceMemberId = "NUEVO",
            CreatedBy = "plan-q-prueba", CreationDate = DateTime.Now
        };
        db.MemberStatisticDeltas.Add(delta);
        await db.SaveChangesAsync();

        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();

        var stats = await db.MemberStatistics.AsNoTracking().Where(s => ids.Contains(s.MemberId)).ToDictionaryAsync(s => s.MemberId);
        ids.Should().OnlyContain(i => stats[i].EnrollmentPoints == 106 && stats[i].EnrollmentTeamSize == 1);
        ids.Select(i => stats[i].QualifiedSponsoredMembers).Should().Equal(Enumerable.Repeat(0, 11).Append(1));
        var encolados = await db.RankEvaluationQueue.AsNoTracking()
            .Where(q => ids.Contains(q.EvaluateMemberId) && !q.IsProcessed).Select(q => q.EvaluateMemberId).ToListAsync();
        encolados.Should().BeEquivalentTo(ids[2..]);
        (await db.MemberStatisticDeltas.AsNoTracking().SingleAsync(d => d.Id == delta.Id)).IsApplied.Should().BeTrue();
        await tx.RollbackAsync();
    }
}
