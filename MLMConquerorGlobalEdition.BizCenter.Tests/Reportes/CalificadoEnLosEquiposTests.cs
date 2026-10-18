using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Mi equipo de inscripción, el detalle de una rama y Mi equipo del Dual Team dicen «Calificado»
/// con la regla de Nick (IQualificationService), no con «la suscripción está activa»: un miembro
/// activo que se inscribió hace dos años sin anualidad ni marca ya no está calificado.
/// </summary>
public class CalificadoEnLosEquiposTests
{
    private const string Yo      = "AMB-000001";
    private const string Antiguo = "AMB-000002";
    private const string Nuevo   = "AMB-000003";

    // Sin estados de cuota en la siembra: nadie tiene la anualidad pagada, como antes del Plan K.
    internal static QualificationService Calificacion(AppDbContext db)
    {
        var reloj = new RelojFijo(new DateTime(2026, 10, 16, 12, 0, 0));
        return new(db, reloj, new BillingStateAnnualFeeStatus(db, reloj));
    }

    internal static RankComputationService Rangos(AppDbContext db) =>
        new(db, new RankQualificationService(db, new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db)));

    private static async Task<AppDbContext> EquipoAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        ReportesDb.Miembro(db, Yo, alta: new DateTime(2023, 1, 1));
        ReportesDb.Miembro(db, Antiguo, Yo, alta: new DateTime(2024, 10, 1));
        ReportesDb.Miembro(db, Nuevo,   Yo, alta: new DateTime(2026, 9, 1));
        ReportesDb.Suscripcion(db, Antiguo, 2, MembershipStatus.Active, new DateTime(2024, 10, 1));
        ReportesDb.Suscripcion(db, Nuevo,   2, MembershipStatus.Active, new DateTime(2026, 9, 1));
        ReportesDb.Nodo(db, Yo,      null, ReportesDb.Ruta(Yo), 1);
        ReportesDb.Nodo(db, Antiguo, Yo,   ReportesDb.Ruta(Yo, Antiguo), 2);
        ReportesDb.Nodo(db, Nuevo,   Yo,   ReportesDb.Ruta(Yo, Nuevo), 2);
        ReportesDb.NodoBinario(db, Yo,      null, ReportesDb.Ruta(Yo), TreeSide.Left);
        ReportesDb.NodoBinario(db, Antiguo, Yo,   ReportesDb.Ruta(Yo, Antiguo), TreeSide.Left);
        ReportesDb.NodoBinario(db, Nuevo,   Yo,   ReportesDb.Ruta(Yo, Nuevo), TreeSide.Right);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Mi_equipo_de_inscripcion_usa_la_regla_de_calificado()
    {
        await using var db = await EquipoAsync();
        var servicio = new EnrollmentTeamService(db, Rangos(db), new EnrollmentTeamPointsService(db), Calificacion(db));

        var filas = (await servicio.GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 50 })).Items
            .ToDictionary(f => f.MemberId, f => f.IsQualified);

        filas[Antiguo].Should().BeFalse("activo, pero inscrito hace dos años sin anualidad ni marca");
        filas[Nuevo].Should().BeTrue();
    }

    [Fact]
    public async Task El_detalle_de_la_rama_usa_la_regla_de_calificado()
    {
        await using var db = await EquipoAsync();
        var servicio = new EnrollmentTeamService(db, Rangos(db), new EnrollmentTeamPointsService(db), Calificacion(db));

        var rama = await servicio.GetBranchDetailAsync(Yo);

        rama!.Ambassadors.Select(a => (a.FullName, a.IsQualified)).Should().BeEquivalentTo(new[]
        {
            ($"Nombre {Antiguo}", false),
            ($"Nombre {Nuevo}", true)
        });
    }

    [Fact]
    public async Task Mi_equipo_del_Dual_Team_usa_la_regla_de_calificado()
    {
        await using var db = await EquipoAsync();
        var servicio = new DualTeamService(db, Rangos(db), Calificacion(db));

        var filas = (await servicio.GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 50 })).Items
            .ToDictionary(f => f.MemberId, f => f.IsQualified);

        filas[Antiguo].Should().BeFalse();
        filas[Nuevo].Should().BeTrue();
    }
}
