using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using static MLMConquerorGlobalEdition.AdminAPI.Tests.Reports.ClubRecognitionTestData;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Vistas de Club Recognition: filtro por vista y nivel, búsqueda por nombre, email o país sin mayúsculas, orden por
/// Reached de la más reciente, sin cuentas corporativas; los textos de CEO Call y evento; los registros de un miembro.
/// </summary>
public class ClubRecognitionReportServiceTests
{
    private static AppDbContext Base()
    {
        var db = Db();
        Niveles(db);
        Opciones(db);
        Evento(db, "EVT-1", "Bootcamp CIS 2026", new DateTime(2026, 3, 10));
        Miembro(db, "AMB-1", "Ana", "Lopez", "ana@example.com", "Bulgaria", "ana");
        Miembro(db, "AMB-2", "Beto", "Diaz", "beto@example.com", "France", "beto");
        Miembro(db, "AMB-3", "Carla", "Ruiz", "carla@example.com", "Japan", null);
        Miembro(db, "AMB-4", "MWR", "Corp.", "info@example.com", "United States", "corp", corporativa: true);

        var r1 = Registro(db, 1, "AMB-1", 1, new DateTime(2026, 11, 13), 55_000m, RecognitionSource.Nightly);
        r1.CeoCallMonth = new DateTime(2026, 10, 1);
        r1.EventId = "EVT-1";
        r1.RewardSize = "US9";
        var r2 = Registro(db, 2, "AMB-2", 4, new DateTime(2026, 3, 5), 110_000m);
        r2.CeoCallOptionId = 3;
        r2.CeoCallReason = "Sponsor request";
        Registro(db, 3, "AMB-3", 1, new DateTime(2026, 6, 1), 51_000m);
        Registro(db, 4, "AMB-3", 4, new DateTime(2026, 7, 1), 101_000m).EventOptionId = 2;
        Registro(db, 5, "AMB-4", 1, new DateTime(2026, 11, 13), 300_000m);   // cuenta corporativa
        db.SaveChanges();
        return db;
    }

    private static ClubRecognitionReportService Servicio(AppDbContext db) => new(db);

    [Theory]
    [InlineData(ClubRecognitionView.All,                 new long[] { 1, 4, 3, 2 })]
    [InlineData(ClubRecognitionView.NotRecognizedOnline, new long[] { 4, 3 })]
    [InlineData(ClubRecognitionView.NotRecognizedEvents, new long[] { 3, 2 })]
    [InlineData(ClubRecognitionView.RecognizedOnline,    new long[] { 1, 2 })]
    [InlineData(ClubRecognitionView.RecognizedEvents,    new long[] { 1, 4 })]
    public async Task Cada_vista_filtra_y_ordena_por_Reached_sin_cuentas_corporativas(ClubRecognitionView vista, long[] ids)
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new ClubRecognitionFilter(vista));

        filas.Select(f => f.Id).Should().Equal(ids);
    }

    [Fact]
    public async Task Filtra_por_nivel()
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new ClubRecognitionFilter(LevelId: 4));

        filas.Select(f => f.Id).Should().Equal(4, 2);
    }

    [Theory]
    [InlineData("japan", new long[] { 4, 3 })]
    [InlineData("BETO", new long[] { 2 })]
    [InlineData("ana@EXAMPLE", new long[] { 1 })]
    [InlineData("lopez", new long[] { 1 })]
    public async Task Busca_por_nombre_email_o_pais_sin_mayusculas(string termino, long[] ids)
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new ClubRecognitionFilter(Search: termino));

        filas.Select(f => f.Id).Should().Equal(ids);
    }

    [Fact]
    public async Task Los_textos_de_CEO_Call_y_evento()
    {
        using var db = Base();

        var filas = (await Servicio(db).GetAllAsync(new ClubRecognitionFilter())).ToDictionary(f => f.Id);

        (filas[1].CeoCallRecognized, filas[1].EventRecognized).Should().Be(("10 2026", "Bootcamp CIS 2026"));
        (filas[2].CeoCallRecognized, filas[2].EventRecognized).Should().Be(("Do not recognize – Sponsor request", (string?)null));
        (filas[4].CeoCallRecognized, filas[4].EventRecognized).Should().Be(((string?)null, "Recognized Long Time Ago"));
        filas[1].Should().BeEquivalentTo(new
        {
            MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Username = "ana", Country = "Bulgaria",
            LevelName = "100k Earner", ReachedDate = new DateTime(2026, 11, 13), QualifyingTotal = 55_000m, RewardSize = "US9"
        });
    }

    [Fact]
    public async Task La_pagina_trae_el_total_filtrado()
    {
        using var db = Base();

        var pagina = await Servicio(db).GetPageAsync(new ClubRecognitionFilter(),
            new GridDataRequest { Page = 2, PageSize = 2 });

        pagina.TotalCount.Should().Be(4);
        pagina.Items.Select(f => f.Id).Should().Equal(3, 2);
    }

    [Fact]
    public async Task Los_registros_de_un_miembro_van_por_nivel_y_la_cuenta_corporativa_ve_los_suyos()
    {
        using var db = Base();

        (await Servicio(db).GetForMemberAsync("AMB-3")).Select(f => f.Id).Should().Equal(3, 4);
        (await Servicio(db).GetForMemberAsync("AMB-4")).Select(f => f.Id).Should().Equal(5);
    }

    [Fact]
    public void Slug_de_cada_vista()
    {
        ClubRecognitionViewNames.Slug(ClubRecognitionView.All).Should().Be("all");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.NotRecognizedOnline).Should().Be("not-recognized-online");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.NotRecognizedEvents).Should().Be("not-recognized-events");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.RecognizedOnline).Should().Be("recognized-online");
        ClubRecognitionViewNames.Slug(ClubRecognitionView.RecognizedEvents).Should().Be("recognized-events");
    }
}
