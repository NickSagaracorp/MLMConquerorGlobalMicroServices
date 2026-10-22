using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Domain.Entities.Loyalty;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Dashboard;
using MLMConquerorGlobalEdition.Repository.Services.Loyalty;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests;

/// <summary>
/// Portada y lealtad en el perfil de miembro del Admin: los mismos servicios que el BizCenter, con el
/// miembro de la ruta, los roles de los datos de equipo del miembro y solo lectura.
/// </summary>
public class AdminMemberDashboardControllerTests
{
    private static AppDbContext NuevaDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Portada_es_la_del_miembro_de_la_ruta()
    {
        var portada = new Mock<IMemberDashboardService>();
        portada.Setup(d => d.GetStatsAsync("AMB-7", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MemberDashboardView { TeamSize = 4 });

        var r = await new AdminMemberDashboardController(portada.Object, Mock.Of<IMemberLoyaltyService>())
            .GetStats("AMB-7");

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<MemberDashboardView>>()
         .Which.Data!.TeamSize.Should().Be(4);
    }

    [Fact]
    public async Task Lealtad_pide_la_pagina_del_miembro_de_la_ruta()
    {
        var lealtad = new Mock<IMemberLoyaltyService>();
        lealtad.Setup(l => l.GetPageAsync("AMB-7", 2, 10, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new LoyaltyPointsPageView { TotalPoints = 90m });

        var r = await new AdminMemberDashboardController(Mock.Of<IMemberDashboardService>(), lealtad.Object)
            .GetLoyalty("AMB-7", 2, 10);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<LoyaltyPointsPageView>>()
         .Which.Data!.TotalPoints.Should().Be(90m);
    }

    [Fact]
    public async Task El_servicio_de_lealtad_totaliza_toda_la_historia_y_no_solo_la_pagina()
    {
        await using var db = NuevaDb();
        db.LoyaltyPoints.AddRange(
            Punto("a", 50m,  locked: false, mes: 1),
            Punto("b", 30m,  locked: true,  mes: 2),
            Punto("c", 20m,  locked: true,  mes: 3),
            Punto("x", 999m, locked: false, mes: 1, miembro: "OTRO"));
        await db.SaveChangesAsync();

        var pagina = await new MemberLoyaltyService(db).GetPageAsync("AMB-7", 1, 2, default);

        pagina.Items.Should().HaveCount(2);
        pagina.TotalCount.Should().Be(3);
        pagina.TotalPoints.Should().Be(100m);
        pagina.UnlockedPoints.Should().Be(50m);
        pagina.LockedPoints.Should().Be(50m);
        pagina.Items.First().MonthNo.Should().Be(3, "el más reciente primero");
    }

    [Fact]
    public async Task El_servicio_de_lealtad_sin_puntos_devuelve_pagina_vacia_con_totales_en_cero()
    {
        await using var db = NuevaDb();

        var pagina = await new MemberLoyaltyService(db).GetPageAsync("AMB-7", 1, 20, default);

        pagina.Items.Should().BeEmpty();
        pagina.TotalPoints.Should().Be(0m);
    }

    private static LoyaltyPoints Punto(string id, decimal puntos, bool locked, int mes, string miembro = "AMB-7") => new()
    {
        Id = id, MemberId = miembro, ProductId = "P", OrderId = "O", PointsEarned = puntos, IsLocked = locked,
        MonthNo = mes, YearNo = 2026, CreationDate = DateTime.Now, CreatedBy = "test", LastUpdateDate = DateTime.Now
    };

    [Fact]
    public void Tiene_los_roles_de_los_datos_de_equipo_del_miembro()
        => typeof(AdminMemberDashboardController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(typeof(AdminMemberEnrollmentController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);

    [Fact]
    public void La_ruta_lleva_el_miembro()
        => typeof(AdminMemberDashboardController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Contain("members/{memberId}");

    [Fact]
    public void Solo_lee()
        => typeof(AdminMemberDashboardController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Should().OnlyContain(m => m == "GET");
}
