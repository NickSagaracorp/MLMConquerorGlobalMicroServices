using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using Xunit.Abstractions;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Club Recognition contra SQL Server: la carga histórica fecha cada registro el día en que se cruzó el umbral (de por vida:
/// el día en que el acumulado llegó; ventana de 180 días: el primer día en que la ventana móvil llegó), respeta lo que ya
/// existe y es idempotente; la detección nocturna se traduce y fecha el día que cerró. Usa los niveles sembrados por la
/// migración (1 = 50.000 en 180 días, 4 = 100.000 de por vida). La última prueba escribe el SQL nocturno para medirlo.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerClubRecognitionTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 0, 45, 0);

    private readonly ITestOutputHelper _salida;

    public SqlServerClubRecognitionTests(ITestOutputHelper salida) => _salida = salida;

    private static ClubRecognitionDetector Detector(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        return new ClubRecognitionDetector(db, reloj.Object);
    }

    private static void Comision(AppDbContext db, string memberId, decimal monto, DateTime fecha,
        CommissionEarningStatus estado = CommissionEarningStatus.Paid) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = memberId, Amount = monto, EarnedDate = fecha, PaymentDate = fecha, Status = estado,
            CommissionTypeId = 1, CreatedBy = "prueba", CreationDate = fecha, LastUpdateDate = fecha
        });

    [SqlServerFact]
    public Task La_carga_historica_fecha_el_dia_en_que_se_cruzo_el_umbral() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        string a = SqlServerReportes.NuevoId("CA"), b = SqlServerReportes.NuevoId("CB"), c = SqlServerReportes.NuevoId("CC"),
               d = SqlServerReportes.NuevoId("CD"), e = SqlServerReportes.NuevoId("CE"), f = SqlServerReportes.NuevoId("CF"),
               g = SqlServerReportes.NuevoId("CG"), h = SqlServerReportes.NuevoId("CH");

        // A: 60.000 el 10-ene (cruza 50k/180d ese día) y 50.000 el 5-mar (el acumulado llega a 110.000: cruza 100k de por vida).
        ReportesDb.Miembro(db, a);
        Comision(db, a, 60_000m, new DateTime(2025, 1, 10, 9, 0, 0));
        Comision(db, a, 50_000m, new DateTime(2025, 3, 5, 15, 0, 0), CommissionEarningStatus.Pending);
        // B: 30.000 el 1-ene sale de la ventana el 30-jun; el 1-jul la ventana tiene 30.000; el 1-ago, 55.000.
        ReportesDb.Miembro(db, b);
        Comision(db, b, 30_000m, new DateTime(2025, 1, 1));
        Comision(db, b, 30_000m, new DateTime(2025, 7, 1));
        Comision(db, b, 25_000m, new DateTime(2025, 8, 1));
        // G: dos comisiones el mismo día suman 60.000 ese día.
        ReportesDb.Miembro(db, g);
        Comision(db, g, 40_000m, new DateTime(2025, 4, 1, 8, 0, 0));
        Comision(db, g, 20_000m, new DateTime(2025, 4, 1, 20, 0, 0));
        // H: ya tiene registro del nivel 1 (nocturno, 9-sep): la carga no lo toca.
        ReportesDb.Miembro(db, h);
        Comision(db, h, 60_000m, new DateTime(2025, 2, 1));
        db.MemberRecognitions.Add(new MemberRecognition
        {
            MemberId = h, RecognitionLevelId = 1, ReachedDate = new DateTime(2025, 9, 9), QualifyingTotal = 60_000m,
            Source = RecognitionSource.Nightly, CreatedBy = "prueba", CreationDate = Ahora
        });
        // No califican: canceladas, cuenta corporativa, Terminated y una comisión posterior al último día.
        ReportesDb.Miembro(db, c);
        Comision(db, c, 120_000m, new DateTime(2025, 2, 1), CommissionEarningStatus.Cancelled);
        ReportesDb.Miembro(db, d).IsCorporateAccount = true;
        Comision(db, d, 120_000m, new DateTime(2025, 2, 1));
        ReportesDb.Miembro(db, e, estado: MemberAccountStatus.Terminated);
        Comision(db, e, 120_000m, new DateTime(2025, 2, 1));
        ReportesDb.Miembro(db, f);
        Comision(db, f, 120_000m, new DateTime(2026, 1, 5));
        await db.SaveChangesAsync();
        var detector = Detector(db);

        var primera = await detector.LoadHistoryAsync(new DateTime(2025, 12, 31));
        var segunda = await detector.LoadHistoryAsync(new DateTime(2025, 12, 31));

        var ids = new[] { a, b, c, d, e, f, g, h };
        var registros = await db.MemberRecognitions.AsNoTracking().Where(r => ids.Contains(r.MemberId)).ToListAsync();
        registros.Select(r => (r.MemberId, r.RecognitionLevelId, r.ReachedDate, r.QualifyingTotal)).Should().BeEquivalentTo(new[]
        {
            (a, 1, new DateTime(2025, 1, 10), 60_000m),
            (a, 4, new DateTime(2025, 3, 5), 110_000m),
            (b, 1, new DateTime(2025, 8, 1), 55_000m),
            (g, 1, new DateTime(2025, 4, 1), 60_000m),
            (h, 1, new DateTime(2025, 9, 9), 60_000m),
        });
        registros.Where(r => r.MemberId != h).Should().OnlyContain(r =>
            r.Source == RecognitionSource.Historical && r.CreatedBy == ClubRecognitionDetector.Author && r.CreationDate == Ahora);
        primera.Should().BeGreaterOrEqualTo(4);          // la base local puede tener más Ambassadors que califiquen
        segunda.Should().Be(0);
        (await detector.HistoryLoadedAsync()).Should().BeTrue();
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task La_deteccion_nocturna_se_traduce_y_fecha_el_dia_que_cerro() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var n = SqlServerReportes.NuevoId("CN");
        ReportesDb.Miembro(db, n);
        Comision(db, n, 120_000m, new DateTime(2026, 11, 13, 9, 0, 0));
        await db.SaveChangesAsync();

        await Detector(db).DetectDayAsync(new DateTime(2026, 11, 13));

        var registros = await db.MemberRecognitions.AsNoTracking().Where(r => r.MemberId == n).ToListAsync();
        registros.Select(r => (r.RecognitionLevelId, r.ReachedDate, r.QualifyingTotal, r.Source)).Should().BeEquivalentTo(new[]
        {
            (1, new DateTime(2026, 11, 13), 120_000m, RecognitionSource.Nightly),
            (4, new DateTime(2026, 11, 13), 120_000m, RecognitionSource.Nightly),
        });
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public void La_consulta_nocturna_se_traduce_a_SQL()
    {
        using var db = SqlServerReportes.Db();
        var hasta = new DateTime(2026, 11, 14);
        foreach (var ventana in new int?[] { 180, null })
        {
            DateTime? desde = ventana is int dias ? hasta.AddDays(-dias) : null;
            var sql = ClubRecognitionDetector.QualifiedTotals(db, desde, hasta, 50_000m).ToQueryString();
            sql.Should().Contain("[MemberProfiles]");
            _salida.WriteLine($"-- ventana {ventana?.ToString() ?? "de por vida"}{Environment.NewLine}{sql}{Environment.NewLine}");
        }
    }
}
