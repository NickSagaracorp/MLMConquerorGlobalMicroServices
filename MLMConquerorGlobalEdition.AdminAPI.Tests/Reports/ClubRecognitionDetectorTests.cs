using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using static MLMConquerorGlobalEdition.AdminAPI.Tests.Reports.ClubRecognitionTestData;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Detección nocturna: el día que cerró, quien llega al umbral de un nivel activo (regla de Earnings, solo Ambassadors no
/// cancelados ni corporativos) y no tiene registro de ese nivel recibe uno con Reached = ese día. Nunca borra ni cambia.
/// </summary>
public class ClubRecognitionDetectorTests
{
    private static readonly DateTime Dia   = new(2026, 11, 13);              // el día que cerró
    private static readonly DateTime Ahora = new(2026, 11, 14, 0, 45, 0);

    private static AppDbContext Base()
    {
        var db = Db();
        Niveles(db);
        // AMB-1: 105.000 de por vida; en la ventana de 180 días (2026-05-18 .. 2026-11-13) solo 45.000.
        Miembro(db, "AMB-1");
        Comision(db, "AMB-1", 60_000m, new DateTime(2026, 1, 10));
        Comision(db, "AMB-1", 45_000m, new DateTime(2026, 11, 13, 18, 0, 0), CommissionEarningStatus.Pending);
        // AMB-2: 55.000 en la ventana.
        Miembro(db, "AMB-2");
        Comision(db, "AMB-2", 30_000m, new DateTime(2026, 10, 1));
        Comision(db, "AMB-2", 25_000m, new DateTime(2026, 11, 13));
        // AMB-3: 260.000 en la ventana: 100k Earner, 500k Earner y 100k Lifetime Earner.
        Miembro(db, "AMB-3");
        Comision(db, "AMB-3", 260_000m, new DateTime(2026, 9, 1));
        // No califican: canceladas, cuenta corporativa, Terminated, cliente y una comisión posterior al día que cerró.
        Miembro(db, "AMB-4");
        Comision(db, "AMB-4", 200_000m, new DateTime(2026, 9, 1), CommissionEarningStatus.Cancelled);
        Miembro(db, "AMB-5", corporativa: true);
        Comision(db, "AMB-5", 200_000m, new DateTime(2026, 9, 1));
        Miembro(db, "AMB-6", estado: MemberAccountStatus.Terminated);
        Comision(db, "AMB-6", 200_000m, new DateTime(2026, 9, 1));
        Miembro(db, "CUS-1", tipo: MemberType.ExternalMember);
        Comision(db, "CUS-1", 200_000m, new DateTime(2026, 9, 1));
        Miembro(db, "AMB-7");
        Comision(db, "AMB-7", 120_000m, new DateTime(2026, 11, 14));
        db.SaveChanges();
        return db;
    }

    private static ClubRecognitionDetector Detector(AppDbContext db) => new(db, Reloj(Ahora));

    private static async Task<List<(string, int, decimal)>> Registros(AppDbContext db) =>
        (await db.MemberRecognitions.AsNoTracking().ToListAsync())
            .Select(r => (r.MemberId, r.RecognitionLevelId, r.QualifyingTotal)).ToList();

    [Fact]
    public async Task Registra_a_quien_cruza_cada_nivel_activo_con_el_dia_que_cerro()
    {
        using var db = Base();

        var nuevos = await Detector(db).DetectDayAsync(Dia);

        nuevos.Should().Be(5);
        (await Registros(db)).Should().BeEquivalentTo(new[]
        {
            ("AMB-1", 4, 105_000m),
            ("AMB-2", 1, 55_000m),
            ("AMB-3", 1, 260_000m),
            ("AMB-3", 2, 260_000m),
            ("AMB-3", 4, 260_000m),
        });
        // CreationDate lo sella AuditInterceptor con DateTime.Now al guardar (no el reloj inyectado).
        (await db.MemberRecognitions.ToListAsync()).Should().OnlyContain(r =>
            r.ReachedDate == Dia && r.Source == RecognitionSource.Nightly
            && r.CreatedBy == ClubRecognitionDetector.Author
            && r.CreationDate > DateTime.Now.AddMinutes(-5) && r.CreationDate <= DateTime.Now.AddMinutes(1));
    }

    [Fact]
    public async Task Correrlo_dos_veces_el_mismo_dia_no_duplica()
    {
        using var db = Base();

        await Detector(db).DetectDayAsync(Dia);
        var segunda = await Detector(db).DetectDayAsync(Dia);

        segunda.Should().Be(0);
        (await db.MemberRecognitions.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Un_registro_que_ya_existe_no_se_toca_ni_se_pierde_aunque_ya_no_califique()
    {
        using var db = Base();
        Registro(db, 99, "AMB-1", 4, new DateTime(2026, 3, 1), 100_000m);
        Miembro(db, "AMB-8");                                                  // calificó en 2025 y hoy no gana nada
        Registro(db, 98, "AMB-8", 1, new DateTime(2025, 1, 1), 50_000m);
        await db.SaveChangesAsync();

        await Detector(db).DetectDayAsync(Dia);

        var amb1 = await db.MemberRecognitions.Where(r => r.MemberId == "AMB-1").ToListAsync();
        amb1.Should().ContainSingle();
        (amb1[0].Id, amb1[0].ReachedDate, amb1[0].QualifyingTotal).Should().Be((99L, new DateTime(2026, 3, 1), 100_000m));
        (await db.MemberRecognitions.AnyAsync(r => r.Id == 98)).Should().BeTrue();
    }

    [Fact]
    public async Task Un_nivel_inactivo_no_se_detecta()
    {
        using var db = Base();
        (await db.RecognitionLevels.SingleAsync(l => l.Id == 4)).IsActive = false;
        await db.SaveChangesAsync();

        await Detector(db).DetectDayAsync(Dia);

        (await db.MemberRecognitions.AnyAsync(r => r.RecognitionLevelId == 4)).Should().BeFalse();
    }

    [Fact]
    public async Task El_marcador_dice_si_ya_se_hizo_la_carga_historica()
    {
        using var db = Base();
        (await Detector(db).HistoryLoadedAsync()).Should().BeFalse();

        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = ClubRecognitionDetector.HistoryLoadedKey, Value = "2026-11-13", CreatedBy = "seed", CreationDate = Ahora
        });
        await db.SaveChangesAsync();

        (await Detector(db).HistoryLoadedAsync()).Should().BeTrue();
    }
}
