using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using Xunit.Abstractions;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Escala;

/// <summary>
/// Plan Q (Tarea 33): la evaluacion de rango (EvaluateRankHandler) en la base de escala, y que el historial de rangos que deja
/// el generador (Scripts/sql/plan-p/escala/altas/h-rangos.sql) es el que daria la evaluacion. Las evaluaciones medidas van en
/// una transaccion que se deshace. Solo con MLM_ESCALA_SQLSERVER y Medicion.runsettings.
/// </summary>
[Trait("Categoria", "Medicion")]
public class EscalaEvaluacionDeRangoTests
{
    private readonly ITestOutputHelper _salida;
    public EscalaEvaluacionDeRangoTests(ITestOutputHelper salida) => _salida = salida;

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private static EvaluateRankHandler Handler(AppDbContext db)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("medicion");
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var trabajos = new Mock<IBackgroundJobClient>();
        trabajos.Setup(c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>())).Returns("medicion");
        return new EvaluateRankHandler(db, new Reloj(), usuario.Object,
            new RankQualificationService(db, new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db)),
            cache.Object, trabajos.Object);
    }

    /// <summary>
    /// ESC-M (el mayor equipo) y el miembro con mas equipo de inscripcion que no tiene el rango mas alto (la evaluacion recorre
    /// todos los rangos que le faltan). Mediana de 3, en una transaccion que se deshace; objetivo menos de 2 s.
    /// </summary>
    [EscalaFact]
    public async Task Evaluar_el_rango_del_mayor_equipo_y_de_un_miembro_sin_el_rango_mas_alto()
    {
        await using var db = EscalaDb.Db(30);
        var maximo = await db.RankDefinitions.Where(r => r.Status == RankDefinitionStatus.Active).MaxAsync(r => r.SortOrder);
        var conTope = db.MemberRankHistories.Where(h => h.RankDefinition!.SortOrder == maximo).Select(h => h.MemberId);
        var otro = await db.MemberStatistics.AsNoTracking()
            .Where(s => s.MemberId != "ESC-M" && s.PersonalPoints > 0 && !conTope.Contains(s.MemberId))
            .OrderByDescending(s => s.EnrollmentTeamSize).ThenBy(s => s.MemberId).Select(s => s.MemberId).FirstAsync();
        var tiempos = new List<TimeSpan>();
        foreach (var id in new[] { "ESC-M", otro })
        {
            string? mensaje = null;
            tiempos.Add(await EscalaDb.MedianaAsync(_salida, $"Evaluar el rango de {id}", async _ =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                var r = await Handler(db).Handle(new EvaluateRankCommand(id), CancellationToken.None);
                r.IsSuccess.Should().BeTrue(r.Error);
                mensaje = r.Value!.Message;
                await tx.RollbackAsync();
                db.ChangeTracker.Clear();
            }, db));
            _salida.WriteLine($"[ESCALA] Evaluar el rango de {id}: {mensaje}");
        }
        tiempos.Should().OnlyContain(t => t < TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// El historial generado es el de la evaluacion: para una muestra de miembros (ESC-M, los 20 de mas equipo y uno de cada
    /// cierto numero por MemberId), los rangos que cumplen segun RankQualificationService (el requisito de menor LevelNo de cada
    /// rango activo con SortOrder > 0, como EvaluateRankHandler) son exactamente los de su historial.
    /// </summary>
    [EscalaFact]
    public async Task El_historial_de_rangos_generado_es_el_de_la_evaluacion()
    {
        await using var db = EscalaDb.Db(120);
        var rangos = await db.RankDefinitions.AsNoTracking().Include(r => r.Requirements)
            .Where(r => r.Status == RankDefinitionStatus.Active && r.SortOrder > 0 && r.Requirements.Count > 0)
            .OrderBy(r => r.SortOrder).ToListAsync();
        var requisitos = rangos.Select(r => r.Requirements.OrderBy(q => q.LevelNo).ThenBy(q => q.Id).First()).ToList();
        var total = await db.MemberProfiles.CountAsync();
        var paso = Math.Max(1, total / 300);
        var muestra = (await db.MemberProfiles.AsNoTracking().OrderBy(m => m.MemberId).Select(m => m.MemberId).ToListAsync())
            .Where((_, i) => i % paso == 0).ToList();
        muestra.Add("ESC-M");
        muestra.AddRange(await db.MemberStatistics.AsNoTracking().OrderByDescending(s => s.EnrollmentTeamSize).ThenBy(s => s.MemberId)
            .Select(s => s.MemberId).Take(20).ToListAsync());
        muestra = muestra.Distinct().ToList();

        var servicio = new RankQualificationService(db, new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db));
        var distintos = new List<string>();
        var conRango = 0;
        foreach (var id in muestra)
        {
            var cumple = (await servicio.QualifiesForAllRanksAsync(id, requisitos))
                .Where(x => x.Result.Qualifies).Select(x => x.Requirement.RankDefinitionId).Order().ToList();
            var guardado = await db.MemberRankHistories.AsNoTracking().Where(h => h.MemberId == id)
                .Select(h => h.RankDefinitionId).OrderBy(x => x).ToListAsync();
            if (guardado.Count > 0) conRango++;
            if (!cumple.SequenceEqual(guardado))
                distintos.Add($"{id}: evaluacion [{string.Join(",", cumple)}], historial [{string.Join(",", guardado)}]");
        }
        _salida.WriteLine($"[ESCALA] Historial de rangos: {muestra.Count} miembros comparados, {conRango} con rango, {distintos.Count} distintos");
        distintos.Should().BeEmpty();
    }
}
