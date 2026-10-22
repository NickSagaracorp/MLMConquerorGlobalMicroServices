using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;
using MLMConquerorGlobalEdition.Domain.Entities.Loyalty;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Loyalty;
using MLMConquerorGlobalEdition.SharedComponents.Components.Loyalty;
using MLMConquerorGlobalEdition.SharedComponents.Constants;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedComponents.Services;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Lealtad;

/// <summary>
/// Puntos de lealtad: el servicio da los registros del miembro y los totales de TODOS sus registros; la
/// pantalla compartida lee exactamente lo que el API devuelve (mes y año, puntos ganados, totales) y solo
/// ofrece el desbloqueo manual si se le da una ruta (el Admin; el BizCenter no tiene esa acción).
/// </summary>
public class PuntosDeLealtadTests : BunitContext
{
    private static AppDbContext NuevaBase() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LoyaltyPoints Registro(string miembro, decimal puntos, int mes, bool bloqueado) => new()
    {
        Id = Guid.NewGuid().ToString(), MemberId = miembro, ProductId = "p", OrderId = "o", PointsEarned = puntos,
        MonthNo = mes, YearNo = 2026, IsLocked = bloqueado, CreationDate = DateTime.Now, CreatedBy = "seed", LastUpdateDate = DateTime.Now
    };

    // -- Servicio ----------------------------------------------------------------

    [Fact]
    public async Task Los_totales_son_de_todos_los_registros_del_miembro_y_no_de_la_pagina()
    {
        using var db = NuevaBase();
        db.LoyaltyPoints.AddRange(Registro("A", 50, 1, false), Registro("A", 30, 2, true), Registro("A", 20, 3, true), Registro("B", 999, 1, false));
        await db.SaveChangesAsync();

        var pagina = await new LoyaltyPointsService(db).GetPageAsync("A", 1, 1);

        pagina.Items.Should().ContainSingle().Which.MonthNo.Should().Be(3);
        pagina.TotalCount.Should().Be(3);
        pagina.TotalPoints.Should().Be(100);
        pagina.UnlockedPoints.Should().Be(50);
        pagina.LockedPoints.Should().Be(50);
    }

    // -- Contrato con la pantalla -------------------------------------------------

    private readonly List<(HttpMethod Metodo, string Ruta, string? Cuerpo)> _llamadas = new();

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed class Manejador(Func<HttpRequestMessage, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => f(r);
    }

    private IRenderedComponent<LoyaltyPointsPage> Montar(LoyaltyPointsPageView datos, string api = "api/v1/bizcenter",
        string? unlock = null, bool permiso = true)
    {
        // El cuerpo es lo que el API serializa de verdad: el contrato se prueba con el tipo real del servidor.
        var json = JsonSerializer.Serialize(ApiResponse<LoyaltyPointsPageView>.Ok(datos), Web);
        Services.AddSharedComponents();
        Services.AddSingleton(new HttpClient(new Manejador(async r =>
        {
            _llamadas.Add((r.Method, r.RequestUri!.PathAndQuery, r.Content is null ? null : await r.Content.ReadAsStringAsync()));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        var vista = new Mock<IViewContextService>();
        vista.Setup(v => v.HasPermission(Permissions.Loyalty.ManualUnlock)).Returns(permiso);
        Services.AddSingleton(vista.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<LoyaltyPointsPage>(p =>
        {
            p.Add(c => c.MemberProfileId, "AMB-7").Add(c => c.ApiBaseUrl, api);
            if (unlock is not null) p.Add(c => c.UnlockUrl, unlock);
        });
    }

    private static LoyaltyPointsPageView Datos() => new()
    {
        Items = new[]
        {
            new LoyaltyPointView { Id = "LP-1", PointsEarned = 50, MonthNo = 10, YearNo = 2026, IsLocked = true },
            new LoyaltyPointView { Id = "LP-2", PointsEarned = 30, MonthNo = 9, YearNo = 2026, IsLocked = false },
        },
        TotalCount = 2, Page = 1, PageSize = 20, TotalPoints = 80, UnlockedPoints = 30, LockedPoints = 50
    };

    [Fact]
    public void Enseña_los_totales_el_mes_y_los_puntos_que_devuelve_el_API()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var p = Montar(Datos());

        p.WaitForAssertion(() => p.FindAll("tbody tr").Should().HaveCount(2));
        var filas = p.FindAll("tbody tr").Select(f => f.TextContent).ToList();
        filas[0].Should().Contain("Oct 2026").And.Contain("50");
        filas[1].Should().Contain("Sep 2026").And.Contain("30");
        p.Markup.Should().Contain(">80<").And.Contain(">30<").And.Contain(">50<");
    }

    [Fact]
    public void Llama_a_loyalty_bajo_la_raiz_que_se_le_da()
    {
        Montar(Datos(), api: "api/v1/admin/members/AMB-7").WaitForAssertion(() =>
            _llamadas.Should().Contain(l => l.Metodo == HttpMethod.Get
                && l.Ruta.StartsWith("/api/v1/admin/members/AMB-7/loyalty?page=1")));
    }

    [Fact]
    public void Sin_ruta_de_desbloqueo_no_hay_boton_aunque_el_visor_tenga_permiso()
    {
        var p = Montar(Datos(), unlock: null, permiso: true);

        p.WaitForAssertion(() => p.FindAll("tbody tr").Should().HaveCount(2));
        p.FindAll("button[title='Loyalty.ManualUnlock']").Should().BeEmpty();
    }

    [Fact]
    public void Con_ruta_y_permiso_el_desbloqueo_manda_el_id_del_registro_a_esa_ruta()
    {
        var p = Montar(Datos(), unlock: "api/v1/admin/loyalty/AMB-7/unlock");
        p.WaitForAssertion(() => p.FindAll("button[title='Loyalty.ManualUnlock']").Should().ContainSingle());

        p.Find("button[title='Loyalty.ManualUnlock']").Click();

        p.WaitForAssertion(() => _llamadas.Should().Contain(l => l.Metodo == HttpMethod.Post
            && l.Ruta == "/api/v1/admin/loyalty/AMB-7/unlock"
            && l.Cuerpo!.Contains("\"loyaltyPointsId\":\"LP-1\"")));
    }

    // -- BizCenter: miembro del token --------------------------------------------

    [Fact]
    public async Task El_BizCenter_devuelve_la_pagina_con_totales_del_miembro_del_token()
    {
        using var db = NuevaBase();
        db.LoyaltyPoints.AddRange(Registro("AMB-7", 40, 1, true), Registro("OTRO", 500, 1, true));
        await db.SaveChangesAsync();
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-7");
        var mediador = new Mock<MediatR.IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<GetLoyaltyPointsQuery>(), It.IsAny<CancellationToken>()))
                .Returns((GetLoyaltyPointsQuery q, CancellationToken ct) =>
                    new GetLoyaltyPointsHandler(new LoyaltyPointsService(db), usuario.Object).Handle(q, ct));

        var r = await new LoyaltyController(mediador.Object).GetLoyaltyPoints();

        var datos = r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<LoyaltyPointsPageView>>().Which.Data!;
        datos.TotalPoints.Should().Be(40);
        datos.LockedPoints.Should().Be(40);
        datos.Items.Should().ContainSingle();
    }
}
