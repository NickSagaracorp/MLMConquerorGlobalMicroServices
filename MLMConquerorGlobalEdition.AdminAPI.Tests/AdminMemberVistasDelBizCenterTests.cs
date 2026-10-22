using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.Repository.Services.Links;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests;

/// <summary>
/// Vistas del BizCenter en el perfil de miembro del Admin (reportes del equipo, eventos, "Mis enlaces"):
/// mismos servicios que el BizCenter, con el miembro de la RUTA, mismos roles que el resto del perfil
/// y solo lectura (el unico POST permitido es el de las rejillas, que leen).
/// </summary>
public class AdminMemberVistasDelBizCenterTests
{
    private static readonly DateTime Desde = new(2026, 10, 1);
    private static readonly DateTime Hasta = new(2026, 10, 15);

    private static T ConContexto<T>(T c) where T : ControllerBase
    {
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return c;
    }

    // -- Reportes del equipo ---------------------------------------------------

    [Fact]
    public void Setup_usa_la_hora_del_servidor()
    {
        var ahora = new DateTime(2026, 10, 21, 9, 30, 0);
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(ahora);

        var r = new AdminMemberTeamReportsController(reloj.Object).GetSetup("AMB-7");

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<ReportSetupView>>()
         .Which.Data.Should().BeEquivalentTo(ReportDateRange.SetupFor(ahora));
    }

    [Fact]
    public async Task Actividad_pide_el_equipo_del_miembro_de_la_ruta()
    {
        var actividad = new Mock<IEnrollmentActivityService>();
        var pedido = new GridDataRequest();
        actividad.Setup(a => a.GetGridAsync("AMB-7", Desde, Hasta, pedido, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PagedResult<EnrollmentActivityRowView>>.Success(new PagedResult<EnrollmentActivityRowView>()));

        var r = await ConContexto(new AdminMemberTeamReportsController(Mock.Of<IDateTimeProvider>()))
            .GetActivityGrid("AMB-7", pedido, Desde, Hasta, actividad.Object);

        r.Should().BeOfType<OkObjectResult>();
        actividad.Verify(a => a.GetGridAsync("AMB-7", Desde, Hasta, pedido, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Actividad_con_rango_invalido_devuelve_400_con_el_codigo_del_servicio()
    {
        var actividad = new Mock<IEnrollmentActivityService>();
        actividad.Setup(a => a.GetGridAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                                            It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PagedResult<EnrollmentActivityRowView>>.Failure("REPORT_RANGE_INVALID", "rango"));

        var r = await ConContexto(new AdminMemberTeamReportsController(Mock.Of<IDateTimeProvider>()))
            .GetActivityGrid("AMB-7", new GridDataRequest(), Hasta, Desde, actividad.Object);

        r.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should()
         .BeOfType<ApiResponse<PagedResult<EnrollmentActivityRowView>>>().Which.ErrorCode.Should().Be("REPORT_RANGE_INVALID");
    }

    [Theory]
    [InlineData("enrollment", StatusChangesScope.Enrollment)]
    [InlineData("dual",       StatusChangesScope.DualTeam)]
    [InlineData("customers",  StatusChangesScope.Customers)]
    public async Task Cambios_de_estado_usan_el_miembro_de_la_ruta_y_el_alcance_pedido(string ruta, StatusChangesScope alcance)
    {
        var cambios = new Mock<ITeamStatusChangesService>();
        var pedido = new GridDataRequest();
        cambios.Setup(c => c.GetGridAsync("AMB-7", alcance, Desde, Hasta, pedido, It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<PagedResult<TeamStatusChangeView>>.Success(new PagedResult<TeamStatusChangeView>()));

        var r = await ConContexto(new AdminMemberTeamReportsController(Mock.Of<IDateTimeProvider>()))
            .GetStatusChangesGrid("AMB-7", ruta, pedido, Desde, Hasta, cambios.Object);

        r.Should().BeOfType<OkObjectResult>();
        cambios.Verify(c => c.GetGridAsync("AMB-7", alcance, Desde, Hasta, pedido, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cambios_de_estado_con_alcance_desconocido_devuelve_404_sin_llamar_al_servicio()
    {
        var cambios = new Mock<ITeamStatusChangesService>(MockBehavior.Strict);

        var r = await ConContexto(new AdminMemberTeamReportsController(Mock.Of<IDateTimeProvider>()))
            .GetStatusChangesGrid("AMB-7", "otro", new GridDataRequest(), Desde, Hasta, cambios.Object);

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    // -- Eventos del equipo ----------------------------------------------------

    [Fact]
    public async Task Inscripciones_del_equipo_son_las_del_miembro_de_la_ruta()
    {
        var eventos = new Mock<ITeamEventRegistrationsService>();
        var pedido = new GridDataRequest();
        eventos.Setup(e => e.GetGridAsync("AMB-7", "EV-1", true, pedido, It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<PagedResult<TeamEventRegistrationView>>.Success(new PagedResult<TeamEventRegistrationView>()));

        var r = await ConContexto(new AdminMemberEventsController(eventos.Object))
            .GetTeamRegistrationsGrid("AMB-7", "EV-1", pedido, true);

        r.Should().BeOfType<OkObjectResult>();
        eventos.Verify(e => e.GetGridAsync("AMB-7", "EV-1", true, pedido, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Totales_de_un_evento_inexistente_devuelven_404()
    {
        var eventos = new Mock<ITeamEventRegistrationsService>();
        eventos.Setup(e => e.GetTotalsAsync("AMB-7", "NO", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<TeamEventTotalsView>.Failure("EVENT_NOT_FOUND", "no existe"));

        var r = await ConContexto(new AdminMemberEventsController(eventos.Object))
            .GetTeamRegistrationsTotals("AMB-7", "NO");

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    // -- Mis enlaces -----------------------------------------------------------

    [Fact]
    public async Task Enlaces_son_los_del_miembro_de_la_ruta()
    {
        var enlaces = new Mock<IMyLinksService>();
        enlaces.Setup(l => l.GetAsync("AMB-7", It.IsAny<CancellationToken>())).ReturnsAsync(new MyLinksView());

        var r = await new AdminMemberLinksController(enlaces.Object).Get("AMB-7");

        r.Should().BeOfType<OkObjectResult>();
        enlaces.Verify(l => l.GetAsync("AMB-7", It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Autorizacion y solo lectura --------------------------------------------

    public static IEnumerable<object[]> Controladores() => new[]
    {
        new object[] { typeof(AdminMemberTeamReportsController) },
        new object[] { typeof(AdminMemberEventsController) },
        new object[] { typeof(AdminMemberLinksController) },
    };

    [Theory, MemberData(nameof(Controladores))]
    public void Tienen_los_roles_de_los_datos_de_equipo_del_miembro(Type controlador)
    {
        var roles = controlador.GetCustomAttribute<AuthorizeAttribute>()!.Roles;
        roles.Should().Be(typeof(AdminMemberEnrollmentController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Theory, MemberData(nameof(Controladores))]
    public void La_ruta_lleva_el_miembro(Type controlador)
        => controlador.GetCustomAttribute<RouteAttribute>()!.Template.Should().Contain("members/{memberId}/");

    [Theory, MemberData(nameof(Controladores))]
    public void Solo_lee_y_el_unico_POST_es_el_de_las_rejillas(Type controlador)
    {
        var metodos = controlador.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var m in metodos)
        {
            var verbos = m.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).ToList();
            verbos.Should().OnlyContain(v => v == "GET" || v == "POST", m.Name);
            if (verbos.Contains("POST")) m.Name.Should().EndWith("Grid", "solo las rejillas leen por POST");
        }
    }
}
