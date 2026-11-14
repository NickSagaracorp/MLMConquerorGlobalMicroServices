using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using static MLMConquerorGlobalEdition.AdminAPI.Controllers.AdminMemberRecognitionController;
using static MLMConquerorGlobalEdition.AdminAPI.Tests.Reports.ClubRecognitionTestData;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>El admin captura CEO Call, evento, talla y notas; cada cambio real deja una fila en GeneralAuditTracking.</summary>
public class AdminMemberRecognitionControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 11, 0, 0);

    private static AppDbContext Base()
    {
        var db = Db();
        Niveles(db, conTalla: 4);                     // 100k Lifetime Earner lleva talla
        Opciones(db);
        Evento(db, "EVT-1", "Bootcamp CIS 2026", new DateTime(2026, 3, 10));
        Miembro(db, "AMB-1", "Ana", "Lopez");
        Miembro(db, "AMB-2", "Beto", "Diaz");
        Registro(db, 10, "AMB-1", 4, new DateTime(2026, 3, 5), 110_000m);
        Registro(db, 11, "AMB-1", 1, new DateTime(2026, 11, 13), 55_000m, RecognitionSource.Nightly);
        Registro(db, 20, "AMB-2", 1, new DateTime(2026, 11, 13), 52_000m, RecognitionSource.Nightly);
        db.SaveChanges();
        return db;
    }

    private static AdminMemberRecognitionController C(AppDbContext db) =>
        new(db, new ClubRecognitionReportService(db), Reloj(Ahora), Usuario());

    private static T Datos<T>(IActionResult r) =>
        ((ApiResponse<T>)r.Should().BeOfType<OkObjectResult>().Subject.Value!).Data!;

    private static string Codigo(IActionResult r) =>
        ((ApiResponse<object>)r.Should().BeOfType<BadRequestObjectResult>().Subject.Value!).ErrorCode!;

    private static CaptureRequest Vacio() => new(null, null, null, null, null, null, null, null);

    [Fact]
    public async Task Captura_mes_evento_talla_y_notas_y_lo_audita()
    {
        using var db = Base();

        var r = await C(db).Capture("AMB-1", 10,
            Vacio() with { CeoCallMonth = new DateTime(2026, 10, 15), EventId = "EVT-1", RewardSize = " US9 ", Notes = " Ring delivered " },
            default);

        var fila = Datos<ClubRecognitionRowView>(r);
        fila.CeoCallMonth.Should().Be(new DateTime(2026, 10, 1));
        (fila.CeoCallRecognized, fila.EventRecognized).Should().Be(("10 2026", "Bootcamp CIS 2026"));
        (fila.RewardSize, fila.Notes).Should().Be(("US9", "Ring delivered"));
        var guardado = await db.MemberRecognitions.AsNoTracking().SingleAsync(x => x.Id == 10);
        (guardado.LastUpdateBy, guardado.LastUpdateDate).Should().Be(("admin-1", (DateTime?)Ahora));
        var audit = await db.AuditTracking.SingleAsync();
        (audit.EntityName, audit.EntityId, audit.Action).Should().Be(("MemberRecognition", "10", "RecognitionCaptured"));
        audit.OldValues.Should().Contain("\"CeoCallMonth\":null").And.Contain("\"EventId\":null");
        audit.NewValues.Should().Contain("\"CeoCallMonth\":\"2026-10-01T00:00:00\"")
            .And.Contain("\"EventId\":\"EVT-1\"").And.Contain("\"RewardSize\":\"US9\"");
        (audit.ChangedBy, audit.ChangedAt).Should().Be(("admin-1", Ahora));
    }

    [Fact]
    public async Task Repetir_los_mismos_valores_no_audita_otra_vez()
    {
        using var db = Base();
        var pedido = Vacio() with { CeoCallMonth = new DateTime(2026, 10, 1), RewardSize = "US9" };

        await C(db).Capture("AMB-1", 10, pedido, default);
        await C(db).Capture("AMB-1", 10, pedido, default);

        (await db.AuditTracking.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Valor_y_opcion_a_la_vez_dan_400()
    {
        using var db = Base();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { CeoCallMonth = new DateTime(2026, 10, 1), CeoCallOptionId = 1 }, default))
            .Should().Be("CEO_CALL_CONFLICT");
        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventId = "EVT-1", EventOptionId = 1 }, default))
            .Should().Be("EVENT_CONFLICT");
        (await db.AuditTracking.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_opcion_que_pide_motivo_lo_exige_y_lo_muestra()
    {
        using var db = Base();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { CeoCallOptionId = 3 }, default)).Should().Be("REASON_REQUIRED");

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 10,
            Vacio() with { CeoCallOptionId = 3, CeoCallReason = "Sponsor request" }, default));
        fila.CeoCallRecognized.Should().Be("Do not recognize – Sponsor request");
    }

    [Fact]
    public async Task Una_opcion_sin_motivo_descarta_el_texto()
    {
        using var db = Base();

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 10,
            Vacio() with { EventOptionId = 1, EventReason = "ignored" }, default));

        (fila.EventReason, fila.EventRecognized).Should().Be(((string?)null, "Recognized Higher Level"));
    }

    [Fact]
    public async Task La_talla_solo_se_guarda_si_el_nivel_la_lleva()
    {
        using var db = Base();

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 11, Vacio() with { RewardSize = "US9" }, default));

        fila.RewardSize.Should().BeNull();
    }

    [Fact]
    public async Task Evento_inexistente_y_opcion_inexistente_dan_400()
    {
        using var db = Base();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventId = "EVT-9" }, default)).Should().Be("EVENT_NOT_FOUND");
        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventOptionId = 9 }, default)).Should().Be("OPTION_NOT_FOUND");
    }


    [Fact]
    public async Task Un_evento_borrado_no_se_asigna_pero_el_que_el_registro_ya_tiene_se_acepta()
    {
        using var db = Base();
        Evento(db, "EVT-DEL", "Evento borrado", new DateTime(2026, 1, 5), borrado: true);
        await db.SaveChangesAsync();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventId = "EVT-DEL" }, default)).Should().Be("EVENT_NOT_FOUND");

        // El registro ya lo tenia (se borro despues): sigue pudiendo editar lo demas sin perderlo.
        var registro = await db.MemberRecognitions.SingleAsync(x => x.Id == 11);
        registro.EventId = "EVT-DEL";
        await db.SaveChangesAsync();
        var r = await C(db).Capture("AMB-1", 11, Vacio() with { EventId = "EVT-DEL", Notes = "ok" }, default);
        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Si_el_registro_ya_no_sale_en_la_lista_responde_404_y_no_500()
    {
        using var db = Base();
        var reportes = new Mock<IClubRecognitionReportService>();
        reportes.Setup(s => s.GetForMemberAsync("AMB-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ClubRecognitionRowView>());
        var c = new AdminMemberRecognitionController(db, reportes.Object, Reloj(Ahora), Usuario());

        var r = await c.Capture("AMB-1", 10, Vacio() with { Notes = "x" }, default);

        r.Should().BeOfType<NotFoundObjectResult>();
    }
    [Fact]
    public async Task Una_opcion_inactiva_no_se_elige_salvo_la_que_el_registro_ya_tiene()
    {
        using var db = Base();
        (await db.MemberRecognitions.SingleAsync(x => x.Id == 11)).EventOptionId = 2;
        (await db.RecognitionOptions.SingleAsync(o => o.Id == 2)).IsActive = false;
        await db.SaveChangesAsync();

        Codigo(await C(db).Capture("AMB-1", 10, Vacio() with { EventOptionId = 2 }, default)).Should().Be("OPTION_NOT_FOUND");
        Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 11, Vacio() with { EventOptionId = 2, Notes = "kept" }, default))
            .EventRecognized.Should().Be("Recognized Long Time Ago");
    }

    [Fact]
    public async Task Vaciar_devuelve_el_registro_a_no_reconocido_y_tambien_se_audita()
    {
        using var db = Base();
        await C(db).Capture("AMB-1", 10, Vacio() with { CeoCallMonth = new DateTime(2026, 10, 1) }, default);

        var fila = Datos<ClubRecognitionRowView>(await C(db).Capture("AMB-1", 10, Vacio(), default));

        fila.CeoCallRecognized.Should().BeNull();
        (await db.AuditTracking.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task El_registro_de_otro_miembro_da_404()
    {
        using var db = Base();

        var r = await C(db).Capture("AMB-1", 20, Vacio() with { Notes = "x" }, default);

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task La_lista_del_miembro_va_por_nivel()
    {
        using var db = Base();

        var filas = Datos<IReadOnlyList<ClubRecognitionRowView>>(await C(db).List("AMB-1", default));

        filas.Select(f => f.Id).Should().Equal(11, 10);
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
        => typeof(AdminMemberRecognitionController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
}
