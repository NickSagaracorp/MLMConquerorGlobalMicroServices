using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CryptoSignups;

/// <summary>
/// La pantalla de aprobación de cobros en cripto, por el lado de AdminAPI.
///
/// Lo que se comprueba aquí es lo que hace rebotar una llamada directa: el atributo de rol. El
/// mismo control existe otra vez en SignupAPI, que es quien de verdad activa la membresía —
/// AdminAPI le reenvía el Bearer del administrador y SignupAPI vuelve a mirarlo por su cuenta—.
/// Así que no hay una puerta trasera por llamar a uno de los dos servicios saltándose el otro.
/// </summary>
public class AdminCryptoSignupsControllerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] LosCuatro =
        [AppRoles.Admin, AppRoles.SuperAdmin, AppRoles.SupportLevel3, AppRoles.BillingManager];

    private static string[] RolesDelControlador() =>
        typeof(AdminCryptoSignupsController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ── Control de acceso ───────────────────────────────────────────────────────

    [Fact]
    public void ElControlador_ExigeAutenticacionYRol()
    {
        var attr = typeof(AdminCryptoSignupsController).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull(
            "aprobar aquí mueve dinero: activa una membresía y dispara comisiones al upline.");
        attr!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers,
            "la lista sale de la constante compartida, no de una cadena escrita a mano.");
    }

    [Fact]
    public void ElControlador_AdmiteExactamenteLosCuatroRolesElegidos()
        => RolesDelControlador().Should().BeEquivalentTo(LosCuatro);

    [Theory]
    [InlineData(AppRoles.CommissionManager)]
    [InlineData(AppRoles.SupportManager)]
    [InlineData(AppRoles.SupportLevel1)]
    [InlineData(AppRoles.SupportLevel2)]
    [InlineData(AppRoles.IT)]
    [InlineData(AppRoles.Ambassador)]
    [InlineData(AppRoles.Member)]
    public void ElControlador_RechazaAlResto(string rolNoAutorizado)
        => RolesDelControlador().Should().NotContain(rolNoAutorizado);

    [Fact]
    public void NingunEndpoint_AflojaElControlConAllowAnonymous()
    {
        var metodos = typeof(AdminCryptoSignupsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        metodos.Should().OnlyContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() == null);
    }

    // ── El listado ──────────────────────────────────────────────────────────────

    private static AdminCryptoSignupsController Build(AppDbContext db)
        => new(db, new Mock<IHttpClientFactory>().Object);

    private static async Task SeedAsync(
        AppDbContext db, string orderId, string memberId, CryptoPaymentConfirmationStatus status)
    {
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId       = memberId,
            Email          = $"{memberId}@example.com",
            FirstName      = "Carlos",
            LastName       = "Rivera",
            MemberType     = MemberType.Ambassador,
            Status         = MemberAccountStatus.Pending,
            EnrollDate     = FixedNow,
            Country        = "US",
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });

        await db.Orders.AddAsync(new Orders
        {
            Id             = orderId,
            MemberId       = memberId,
            OrderNo        = $"NO-{memberId}",
            TotalAmount    = 80,
            Status         = status == CryptoPaymentConfirmationStatus.AwaitingPayment
                                ? OrderStatus.Processing : OrderStatus.Completed,
            OrderDate      = FixedNow,
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });

        await db.CryptoPaymentConfirmations.AddAsync(new CryptoPaymentConfirmation
        {
            OrderId             = orderId,
            MemberId            = memberId,
            MemberEmail         = $"{memberId}@example.com",
            CryptoCurrency      = "BTC",
            AmountDue           = 80,
            Status              = status,
            CryptoTransactionId = status == CryptoPaymentConfirmationStatus.Confirmed ? "abc123" : null,
            ConfirmedByEmail    = status == CryptoPaymentConfirmationStatus.Confirmed ? "admin@x.com" : null,
            ConfirmedAt         = status == CryptoPaymentConfirmationStatus.Confirmed ? FixedNow : null,
            CreatedBy           = "seed",
            CreationDate        = FixedNow,
            LastUpdateDate      = FixedNow
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ListPending_DevuelveSoloLosQueEsperanCobro()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-A", "AMB-A", CryptoPaymentConfirmationStatus.AwaitingPayment);
        await SeedAsync(db, "ORD-B", "AMB-B", CryptoPaymentConfirmationStatus.Confirmed);

        var result = await Build(db).ListPending();

        var ok   = result.Should().BeOfType<OkObjectResult>().Subject;
        var resp = ok.Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminCryptoSignupsController.PendingCryptoSignupDto>>>().Subject;

        resp.Success.Should().BeTrue();
        resp.Data!.Should().HaveCount(1);
        resp.Data!.Single().OrderId.Should().Be("ORD-A");
        resp.Data!.Single().CryptoCurrency.Should().Be("BTC");
        resp.Data!.Single().AmountDue.Should().Be(80);
    }

    [Fact]
    public async Task ListConfirmed_DevuelveElRastroDeAuditoria()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-A", "AMB-A", CryptoPaymentConfirmationStatus.AwaitingPayment);
        await SeedAsync(db, "ORD-B", "AMB-B", CryptoPaymentConfirmationStatus.Confirmed);

        var result = await Build(db).ListConfirmed();

        var ok   = result.Should().BeOfType<OkObjectResult>().Subject;
        var resp = ok.Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminCryptoSignupsController.ConfirmedCryptoSignupDto>>>().Subject;

        var fila = resp.Data!.Single();
        fila.OrderId.Should().Be("ORD-B");
        fila.CryptoTransactionId.Should().Be("abc123");
        fila.ConfirmedByEmail.Should().Be("admin@x.com");
        fila.ConfirmedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Confirm_DeUnPedidoQueNoExiste_DevuelveNotFoundSinLlamarASignupApi()
    {
        await using var db = InMemoryDbHelper.Create();
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);

        var controller = new AdminCryptoSignupsController(db, factory.Object);

        var result = await controller.Confirm(
            "ORD-QUE-NO-EXISTE",
            new AdminCryptoSignupsController.ConfirmCryptoRequest("abc123", null));

        result.Should().BeOfType<NotFoundObjectResult>();
        factory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }
}
