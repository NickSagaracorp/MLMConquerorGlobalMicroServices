using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CryptoSignups;

/// <summary>
/// Pagos de Entrax del alta en el admin: los que esperan, los que hay que revisar y los recientes;
/// Re-check se reenvía a SignupAPI con el Bearer del administrador (SignupAPI vuelve a comprobar el rol)
/// y queda en AuditTracking con quién lo pidió y qué contestó SignupAPI.
/// </summary>
public class AdminEntraxSignupsControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 10, 0, 0);

    private sealed class Captura(HttpStatusCode codigo = HttpStatusCode.OK,
        string cuerpo = "{\"success\":true,\"data\":{\"outcome\":\"Activated\"}}") : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticion = r;
            return Task.FromResult(new HttpResponseMessage(codigo)
            {
                Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Caido(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(ex);
    }

    private static Mock<IHttpClientFactory> Fabrica(HttpMessageHandler h)
    {
        var f = new Mock<IHttpClientFactory>();
        f.Setup(x => x.CreateClient("SignupApi")).Returns(new HttpClient(h) { BaseAddress = new Uri("https://signup.pruebas/") });
        return f;
    }

    private static AdminEntraxSignupsController Build(AppDbContext db, IHttpClientFactory? f = null, string? bearer = null)
    {
        var ctx = new DefaultHttpContext();
        if (bearer is not null) ctx.Request.Headers.Authorization = bearer;

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns("admin-uid-1");
        user.Setup(u => u.Email).Returns("billing@example.com");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);

        return new AdminEntraxSignupsController(db, f ?? Mock.Of<IHttpClientFactory>(), user.Object, clock.Object,
            NullLogger<AdminEntraxSignupsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    private static async Task SeedAsync(AppDbContext db, string id, EntraxPaymentStatus status,
        string memberId = "AMB-RU-1", DateTime? creado = null)
    {
        if (!db.MemberProfiles.Any())
            db.MemberProfiles.Add(new MemberProfile
            {
                MemberId = "AMB-RU-1", Email = "ivan@example.com", FirstName = "Ivan", LastName = "Petrov",
                MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Pending, EnrollDate = Now,
                Country = "RU", CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
            });
        db.EntraxPayments.Add(new EntraxPayment
        {
            Id = id, OrderId = $"ORD-{id}", MemberId = memberId, ExternalOrderId = $"SIGNUP-ORD-{id}-1",
            ExternalPaymentId = Guid.NewGuid().ToString(), Status = status, AmountUsd = 80m, AmountRub = 7640m,
            QrLink = "https://qr.nspk.ru/secreto-de-pago",
            Notes = "nota interna",
            FailureCode = status == EntraxPaymentStatus.ToReview ? "AMOUNT_TOO_LOW" : null,
            CreatedBy = "seed", CreationDate = creado ?? Now, LastUpdateDate = Now
        });
        await db.SaveChangesAsync();
    }

    private static PagedResult<AdminEntraxSignupsController.EntraxSignupRowDto> Pagina(IActionResult r)
        => ((OkObjectResult)r).Value.Should()
            .BeOfType<ApiResponse<PagedResult<AdminEntraxSignupsController.EntraxSignupRowDto>>>().Which.Data!;

    private static IEnumerable<AdminEntraxSignupsController.EntraxSignupRowDto> Filas(IActionResult r) => Pagina(r).Items;

    // ── Quién puede llamar ───────────────────────────────────────────────────────

    [Fact]
    public void Exige_los_roles_de_cripto_y_nada_es_anonimo()
    {
        typeof(AdminEntraxSignupsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.CryptoPaymentApprovers);
        typeof(AdminEntraxSignupsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().OnlyContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() == null);
    }

    // ── Lista ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Por_defecto_muestra_los_que_esperan_pago()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);
        await SeedAsync(db, "p2", EntraxPaymentStatus.ToReview);
        await SeedAsync(db, "p3", EntraxPaymentStatus.Paid);

        var fila = Filas(await Build(db).List()).Should().ContainSingle().Subject;

        fila.Id.Should().Be("p1");
        fila.Status.Should().Be("AwaitingPayment");
        fila.FullName.Should().Be("Ivan Petrov");
        fila.Country.Should().Be("RU");
    }

    [Fact]
    public async Task A_revisar_no_pierde_un_pago_sin_perfil()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.ToReview, memberId: "AMB-BORRADO");

        var fila = Filas(await Build(db).List(AdminEntraxSignupsController.ViewReview)).Should()
            .ContainSingle("un pago a revisar tiene dinero en juego: no puede desaparecer de la lista.").Subject;

        fila.Reason.Should().Be("AMOUNT_TOO_LOW");
        fila.FullName.Should().BeNull();
    }

    [Fact]
    public async Task Recientes_muestra_todos_incluidos_los_sustituidos()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);
        await SeedAsync(db, "p2", EntraxPaymentStatus.Paid);
        await SeedAsync(db, "p3", EntraxPaymentStatus.Superseded);

        var filas = Filas(await Build(db).List(AdminEntraxSignupsController.ViewRecent)).ToList();

        filas.Should().HaveCount(3);
        filas.Single(f => f.Id == "p3").Status.Should().Be("Superseded");
    }

    [Fact]
    public async Task Pagina_de_mas_reciente_a_mas_antiguo_con_el_total()
    {
        await using var db = InMemoryDbHelper.Create();
        for (var i = 1; i <= 5; i++)
            await SeedAsync(db, $"p{i}", EntraxPaymentStatus.Paid, creado: Now.AddMinutes(i));

        var pagina = Pagina(await Build(db).List(AdminEntraxSignupsController.ViewRecent, page: 2, pageSize: 2));

        pagina.TotalCount.Should().Be(5);
        pagina.Page.Should().Be(2);
        pagina.PageSize.Should().Be(2);
        pagina.Items.Select(f => f.Id).Should().Equal("p3", "p2");
    }

    [Theory]
    [InlineData(0, 0, 1, AdminEntraxSignupsController.DefaultPageSize)]
    [InlineData(-3, 100000, 1, AdminEntraxSignupsController.MaxPageSize)]
    public async Task Pagina_y_tamano_fuera_de_rango_se_corrigen(int page, int size, int paginaEsperada, int tamanoEsperado)
    {
        await using var db = InMemoryDbHelper.Create();

        var pagina = Pagina(await Build(db).List(AdminEntraxSignupsController.ViewRecent, page, size));

        pagina.Page.Should().Be(paginaEsperada);
        pagina.PageSize.Should().Be(tamanoEsperado);
    }

    [Fact]
    public void La_fila_no_lleva_el_enlace_del_QR_ni_notas_internas()
    {
        var props = typeof(AdminEntraxSignupsController.EntraxSignupRowDto).GetProperties().Select(p => p.Name);

        props.Should().NotContain(new[] { "QrLink", "Notes", "Secret", "Signature", "Body" },
            "el enlace del QR es cobrable y la fila solo lleva lo que necesita quien revisa.");
    }

    // ── Re-check ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Re_check_se_reenvia_a_SignupAPI_con_el_Bearer_del_administrador()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);
        var captura = new Captura();

        var r = await Build(db, Fabrica(captura).Object, "Bearer token-admin").Recheck("p1");

        captura.Peticion!.Method.Should().Be(HttpMethod.Post);
        captura.Peticion.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/entrax/p1/recheck");
        captura.Peticion.Headers.Authorization!.ToString().Should().Be("Bearer token-admin");
        var contenido = r.Should().BeOfType<ContentResult>().Subject;
        contenido.StatusCode.Should().Be(200);
        contenido.Content.Should().Contain("Activated");
    }

    [Fact]
    public async Task Re_check_queda_auditado_con_quien_lo_pidio_y_lo_que_contesto_SignupAPI()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);

        await Build(db, Fabrica(new Captura()).Object, "Bearer token-admin").Recheck("p1");

        var fila = db.AuditTracking.Should().ContainSingle().Subject;
        fila.EntityName.Should().Be(nameof(EntraxPayment));
        fila.EntityId.Should().Be("p1");
        fila.Action.Should().Be(AdminEntraxSignupsController.AuditRecheck);
        fila.ChangedBy.Should().Be("admin-uid-1");
        fila.ChangedAt.Should().Be(Now);
        fila.OldValues.Should().Contain("AwaitingPayment");
        using var nuevo = JsonDocument.Parse(fila.NewValues!);
        nuevo.RootElement.GetProperty("HttpStatus").GetInt32().Should().Be(200);
        nuevo.RootElement.GetProperty("Outcome").GetString().Should().Be("Activated");
        fila.NewValues.Should().NotContain("token-admin", "el Bearer no se guarda en ningún sitio.");
    }

    [Fact]
    public async Task Re_check_con_error_de_SignupAPI_audita_su_codigo_y_devuelve_su_respuesta()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);
        var captura = new Captura(HttpStatusCode.Forbidden, "{\"success\":false,\"errorCode\":\"FORBIDDEN\"}");

        var r = await Build(db, Fabrica(captura).Object, "Bearer token-admin").Recheck("p1");

        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(403);
        using var nuevo = JsonDocument.Parse(db.AuditTracking.Single().NewValues!);
        nuevo.RootElement.GetProperty("HttpStatus").GetInt32().Should().Be(403);
        nuevo.RootElement.GetProperty("Outcome").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Re_check_de_un_pago_que_no_existe_no_llama_a_nadie_ni_audita()
    {
        await using var db = InMemoryDbHelper.Create();
        var fabrica = new Mock<IHttpClientFactory>(MockBehavior.Strict);

        (await Build(db, fabrica.Object).Recheck("no-existe")).Should().BeOfType<NotFoundObjectResult>();
        db.AuditTracking.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_SignupAPI_caido_es_502_y_queda_auditado()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);

        var r = await Build(db, Fabrica(new Caido(new HttpRequestException("caido"))).Object).Recheck("p1");

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        using var nuevo = JsonDocument.Parse(db.AuditTracking.Single().NewValues!);
        nuevo.RootElement.GetProperty("Outcome").GetString().Should().Be("SIGNUP_API_UNAVAILABLE");
    }

    [Fact]
    public async Task Con_SignupAPI_sin_responder_a_tiempo_es_502()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "p1", EntraxPaymentStatus.AwaitingPayment);

        var r = await Build(db, Fabrica(new Caido(new TaskCanceledException("timeout"))).Object).Recheck("p1");

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }
}
