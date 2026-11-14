using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using static MLMConquerorGlobalEdition.AdminAPI.Controllers.AdminClubRecognitionParametersController;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminClubRecognitionParametersControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 0, 0);

    private static AdminClubRecognitionParametersController Controlador(AppDbContext db) =>
        new(db, ClubRecognitionTestData.Reloj(Ahora), ClubRecognitionTestData.Usuario());

    private static T Datos<T>(IActionResult r) =>
        ((ApiResponse<T>)r.Should().BeOfType<OkObjectResult>().Subject.Value!).Data!;

    private static string Codigo(IActionResult r) =>
        ((ApiResponse<object>)r.Should().BeOfType<BadRequestObjectResult>().Subject.Value!).ErrorCode!;

    [Fact]
    public async Task Crear_un_nivel_lo_guarda_limpio_con_autor_y_hora_del_servidor()
    {
        using var db = ClubRecognitionTestData.Db();

        var r = await Controlador(db).CreateLevel(
            new LevelRequest("  Diamond Club  ", 2_000_000m, null, " Ring ", true, 7), default);

        var dto = Datos<LevelDto>(r);
        dto.Should().Be(new LevelDto(dto.Id, "Diamond Club", 2_000_000m, null, "Ring", true, 7, true));
        var guardado = await db.RecognitionLevels.SingleAsync();
        guardado.CreatedBy.Should().Be("admin-1");
        // El AuditInterceptor del contexto fija CreationDate con la hora local del servidor al guardar (no la del reloj).
        guardado.CreationDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData("", 1000, null, "INVALID_NAME")]
    [InlineData("Club", 0, null, "INVALID_THRESHOLD")]
    [InlineData("Club", 1000, 0, "INVALID_WINDOW")]
    [InlineData("Club", 1000, 3651, "INVALID_WINDOW")]
    public async Task Un_nivel_invalido_da_400(string nombre, int umbral, int? ventana, string codigo)
    {
        using var db = ClubRecognitionTestData.Db();

        var r = await Controlador(db).CreateLevel(new LevelRequest(nombre, umbral, ventana, null, false, 1), default);

        Codigo(r).Should().Be(codigo);
        (await db.RecognitionLevels.CountAsync()).Should().Be(0);
    }


    [Theory]
    [InlineData("100000000000000")]
    [InlineData("999999999999999999")]
    public async Task Un_umbral_que_no_cabe_en_la_columna_da_400(string umbral)
    {
        using var db = ClubRecognitionTestData.Db();

        var r = await Controlador(db).CreateLevel(new LevelRequest("Club", decimal.Parse(umbral), null, null, false, 1), default);

        Codigo(r).Should().Be("INVALID_THRESHOLD");
    }

    private sealed class FalloDeClaveDuplicada : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            var error = (SqlError)RuntimeHelpers.GetUninitializedObject(typeof(SqlError));
            typeof(SqlError).GetField("_number", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(error, 2601);
            var lista = (SqlErrorCollection)RuntimeHelpers.GetUninitializedObject(typeof(SqlErrorCollection));
            typeof(SqlErrorCollection).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(lista, new List<object> { error });
            var sql = (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));
            typeof(SqlException).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(sql, lista);
            throw new DbUpdateException("duplicate", sql);
        }
    }

    [Fact]
    public async Task Una_clave_duplicada_al_guardar_da_400_y_no_500()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new FalloDeClaveDuplicada()).Options);
        var c = Controlador(db);

        Codigo(await c.CreateLevel(new LevelRequest("Club", 1000m, null, null, false, 1), default)).Should().Be("LEVEL_NAME_TAKEN");
        Codigo(await c.CreateOption(new OptionRequest("Opcion", false, 1), default)).Should().Be("OPTION_NAME_TAKEN");
    }
    [Fact]
    public async Task Dos_niveles_no_pueden_llamarse_igual()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Niveles(db);
        await db.SaveChangesAsync();

        var r = await Controlador(db).CreateLevel(new LevelRequest("100k Earner", 1m, null, null, false, 9), default);

        Codigo(r).Should().Be("LEVEL_NAME_TAKEN");
    }

    [Fact]
    public async Task Editar_un_nivel_cambia_sus_campos_y_deja_quien_y_cuando()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Niveles(db);
        await db.SaveChangesAsync();

        var r = await Controlador(db).UpdateLevel(4,
            new LevelRequest("100k Lifetime Earner", 120_000m, null, "Ring", true, 4, IsActive: false), default);

        Datos<LevelDto>(r).Should().Be(new LevelDto(4, "100k Lifetime Earner", 120_000m, null, "Ring", true, 4, false));
        var nivel = await db.RecognitionLevels.SingleAsync(l => l.Id == 4);
        nivel.LastUpdateBy.Should().Be("admin-1");
        // El AuditInterceptor del contexto fija LastUpdateDate con la hora local del servidor al guardar (no la del reloj).
        nivel.LastUpdateDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Editar_un_nivel_que_no_existe_da_404()
    {
        using var db = ClubRecognitionTestData.Db();

        var r = await Controlador(db).UpdateLevel(99, new LevelRequest("X", 1m, null, null, false, 1), default);

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Crear_y_editar_una_opcion()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Opciones(db);
        await db.SaveChangesAsync();

        var creada = Datos<OptionDto>(await Controlador(db).CreateOption(new OptionRequest(" Recognized by video ", false, 4), default));
        creada.Should().Be(new OptionDto(creada.Id, "Recognized by video", false, 4, true));

        var editada = Datos<OptionDto>(await Controlador(db).UpdateOption(3,
            new OptionRequest("Do not recognize", true, 3, IsActive: false), default));
        editada.Should().Be(new OptionDto(3, "Do not recognize", true, 3, false));

        Codigo(await Controlador(db).CreateOption(new OptionRequest("Recognized Higher Level", false, 5), default))
            .Should().Be("OPTION_NAME_TAKEN");
        Codigo(await Controlador(db).CreateOption(new OptionRequest(" ", false, 5), default))
            .Should().Be("INVALID_NAME");
    }

    [Fact]
    public async Task Las_listas_traen_niveles_por_orden_todas_las_opciones_y_los_eventos_vigentes_del_mas_reciente()
    {
        using var db = ClubRecognitionTestData.Db();
        ClubRecognitionTestData.Niveles(db);
        ClubRecognitionTestData.Opciones(db);
        (await db.SaveChangesAsync()).Should().BeGreaterThan(0);
        (await db.RecognitionOptions.SingleAsync(o => o.Id == 2)).IsActive = false;
        ClubRecognitionTestData.Evento(db, "EVT-1", "Bootcamp CIS 2026", new DateTime(2026, 3, 10));
        ClubRecognitionTestData.Evento(db, "EVT-2", "Leadership Summit 2026", new DateTime(2026, 9, 20));
        ClubRecognitionTestData.Evento(db, "EVT-3", "Cancelled Gala", new DateTime(2026, 10, 1), borrado: true);
        await db.SaveChangesAsync();

        var listas = Datos<LookupsDto>(await Controlador(db).Lookups(default));

        listas.Levels.Select(l => l.Id).Should().Equal(1, 2, 3, 4, 5, 6);
        listas.Options.Select(o => (o.Id, o.IsActive)).Should().Equal((1, true), (2, false), (3, true));
        listas.Events.Select(e => e.Id).Should().Equal("EVT-2", "EVT-1");
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
        => typeof(AdminClubRecognitionParametersController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
}
