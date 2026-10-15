using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// AdminAPI lista las compras de tokens en cripto pendientes y las que hay que conciliar, y reenvía
/// las acciones a Billing con el Bearer del administrador. Billing vuelve a comprobar el rol.
/// </summary>
public class AdminTokenPurchasesControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private sealed class Captura : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;
        public string? Cuerpo;
        public HttpStatusCode Respuesta = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticion = r;
            Cuerpo   = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(Respuesta)
            {
                Content = new StringContent("{\"success\":true}", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static async Task SeedAsync(AppDbContext db, string orderId, TokenPurchaseStatus status,
        CryptoPaymentPurpose? crypto = CryptoPaymentPurpose.TokenPurchase,
        CryptoPaymentConfirmationStatus cryptoStatus = CryptoPaymentConfirmationStatus.AwaitingPayment)
    {
        if (!db.MemberProfiles.Any())
            db.MemberProfiles.Add(new MemberProfile
            {
                MemberId = "AMB-1", Email = "a@x.com", FirstName = "Ana", LastName = "Ruiz", MemberType = MemberType.Ambassador,
                Status = MemberAccountStatus.Active, EnrollDate = Now, Country = "US", CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
            });
        if (!db.TokenTypes.Any())
            db.TokenTypes.Add(new TokenType { Id = 13, Name = "VIP Member", IsActive = true, CreatedBy = "seed", CreationDate = Now });
        db.Orders.Add(new Orders { Id = orderId, MemberId = "AMB-1", OrderNo = $"NO-{orderId}", TotalAmount = 99.99m, Status = OrderStatus.Processing, OrderDate = Now, CreatedBy = "seed", CreationDate = Now });
        if (crypto is not null)
            db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
            {
                OrderId = orderId, MemberId = "AMB-1", MemberEmail = "a@x.com", CryptoCurrency = "USDT",
                AmountDue = 99.99m, Purpose = crypto.Value, Status = cryptoStatus, CreatedBy = "seed", CreationDate = Now
            });
        if (crypto != CryptoPaymentPurpose.Signup)
            db.TokenPurchases.Add(new TokenPurchase
            {
                Id = $"p-{orderId}", MemberId = "AMB-1", TokenTypeId = 13, GrantedProductIds = "prod-vip", Quantity = 1,
                UnitPrice = 99.99m, TotalAmount = 99.99m,
                PaymentMethod = crypto is null ? TokenPaymentMethod.EWallet : TokenPaymentMethod.Crypto,
                Status = status, OrderId = orderId, IdempotencyKey = orderId, PeriodKey = 202610,
                FailureCode = status == TokenPurchaseStatus.NeedsReconciliation ? "EWALLET_DEBIT_PENDING" : null,
                CreatedBy = "seed", CreationDate = Now
            });
        await db.SaveChangesAsync();
    }

    private static AdminTokenPurchasesController Build(AppDbContext db, IHttpClientFactory? f = null, string? bearer = null)
    {
        var ctx = new DefaultHttpContext();
        if (bearer is not null) ctx.Request.Headers.Authorization = bearer;
        return new AdminTokenPurchasesController(db, f ?? Mock.Of<IHttpClientFactory>())
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    private static (Mock<IHttpClientFactory> Factory, Captura Captura) Billing(HttpStatusCode status = HttpStatusCode.OK)
    {
        var captura = new Captura { Respuesta = status };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("BillingApi"))
               .Returns(new HttpClient(captura) { BaseAddress = new Uri("https://billing.pruebas/") });
        return (factory, captura);
    }

    // ── Control de acceso ───────────────────────────────────────────────────────

    [Fact]
    public void Roles_de_cada_accion()
    {
        typeof(AdminTokenPurchasesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.CryptoPaymentApprovers);
        typeof(AdminTokenPurchasesController).GetMethod(nameof(AdminTokenPurchasesController.Reconcile))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin,BillingManager");
        typeof(AdminTokenPurchasesController).GetMethod(nameof(AdminTokenPurchasesController.ListToReview))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin,BillingManager");
    }

    [Fact]
    public void Ningun_endpoint_afloja_el_control_con_AllowAnonymous()
        => typeof(AdminTokenPurchasesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().OnlyContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() == null);

    // ── Listados ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cripto_pendiente_solo_muestra_compras_de_tokens()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-SIGNUP", TokenPurchaseStatus.AwaitingCryptoConfirmation, CryptoPaymentPurpose.Signup);
        await SeedAsync(db, "ORD-TOKENS", TokenPurchaseStatus.AwaitingCryptoConfirmation);

        var r = await Build(db).ListCryptoPending();

        var fila = ((OkObjectResult)r).Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminTokenPurchasesController.PendingCryptoTokenPurchaseDto>>>()
            .Which.Data!.Single();
        fila.OrderId.Should().Be("ORD-TOKENS");
        fila.PurchaseId.Should().Be("p-ORD-TOKENS");
        fila.TokenTypeName.Should().Be("VIP Member");
        fila.CryptoCurrency.Should().Be("USDT");
    }

    [Fact]
    public async Task Cripto_pendiente_no_muestra_las_canceladas_rechazadas_ni_caducadas()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-CAN", TokenPurchaseStatus.Cancelled, cryptoStatus: CryptoPaymentConfirmationStatus.Cancelled);
        await SeedAsync(db, "ORD-EXP", TokenPurchaseStatus.Expired, cryptoStatus: CryptoPaymentConfirmationStatus.Cancelled);
        await SeedAsync(db, "ORD-OK", TokenPurchaseStatus.Completed, cryptoStatus: CryptoPaymentConfirmationStatus.Confirmed);

        var r = await Build(db).ListCryptoPending();

        ((OkObjectResult)r).Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminTokenPurchasesController.PendingCryptoTokenPurchaseDto>>>()
            .Which.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task A_revisar_muestra_las_que_esperan_conciliacion()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-EW", TokenPurchaseStatus.NeedsReconciliation, crypto: null);
        await SeedAsync(db, "ORD-OK", TokenPurchaseStatus.Completed, crypto: null);

        var r = await Build(db).ListToReview();

        var fila = ((OkObjectResult)r).Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminTokenPurchasesController.TokenPurchaseToReviewDto>>>()
            .Which.Data!.Single();
        fila.PurchaseId.Should().Be("p-ORD-EW");
        fila.Reason.Should().Be("EWALLET_DEBIT_PENDING");
        fila.PaymentMethod.Should().Be("EWallet");
    }

    // ── Reenvío a Billing ───────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_reenvia_a_Billing_con_el_Bearer_del_admin()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-TOKENS", TokenPurchaseStatus.AwaitingCryptoConfirmation);
        var (factory, captura) = Billing();

        var r = await Build(db, factory.Object, "Bearer admin.jwt")
            .ConfirmCrypto("ORD-TOKENS", new AdminTokenPurchasesController.ConfirmCryptoRequest("0xabc", null));

        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(200);
        captura.Peticion!.Method.Should().Be(HttpMethod.Post);
        captura.Peticion.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/crypto/ORD-TOKENS/confirm");
        captura.Peticion.Headers.Authorization!.ToString().Should().Be("Bearer admin.jwt");
        captura.Cuerpo.Should().Contain("0xabc");
    }

    [Fact]
    public async Task El_codigo_de_Billing_llega_tal_cual()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-TOKENS", TokenPurchaseStatus.AwaitingCryptoConfirmation);
        var (factory, _) = Billing(HttpStatusCode.Conflict);

        var r = await Build(db, factory.Object, "Bearer admin.jwt")
            .ConfirmCrypto("ORD-TOKENS", new AdminTokenPurchasesController.ConfirmCryptoRequest("0xabc", null));

        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Conciliar_reenvia_a_Billing()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-EW", TokenPurchaseStatus.NeedsReconciliation, crypto: null);
        var (factory, captura) = Billing();

        await Build(db, factory.Object, "Bearer admin.jwt")
            .Reconcile("p-ORD-EW", new AdminTokenPurchasesController.ReconcileRequest(true, "ipayout-tx-9", null));

        captura.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/p-ORD-EW/reconcile");
        captura.Peticion.Headers.Authorization!.ToString().Should().Be("Bearer admin.jwt");
        captura.Cuerpo.Should().Contain("ipayout-tx-9").And.Contain("true");
    }

    [Fact]
    public async Task Conciliar_algo_que_no_esta_a_revisar_no_se_reenvia()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-OK", TokenPurchaseStatus.Completed, crypto: null);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);

        var r = await Build(db, factory.Object)
            .Reconcile("p-ORD-OK", new AdminTokenPurchasesController.ReconcileRequest(false, null, null));

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Rechazar_reenvia_a_Billing_con_el_motivo()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-TOKENS", TokenPurchaseStatus.AwaitingCryptoConfirmation);
        var (factory, captura) = Billing();

        var r = await Build(db, factory.Object, "Bearer admin.jwt")
            .RejectCrypto("ORD-TOKENS", new AdminTokenPurchasesController.RejectCryptoRequest("No llegó"));

        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(200);
        captura.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/crypto/ORD-TOKENS/reject");
        captura.Cuerpo.Should().Contain("No lleg");
    }

    [Fact]
    public async Task Confirmar_un_alta_no_se_reenvia()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-SIGNUP", TokenPurchaseStatus.AwaitingCryptoConfirmation, CryptoPaymentPurpose.Signup);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);

        var r = await Build(db, factory.Object)
            .ConfirmCrypto("ORD-SIGNUP", new AdminTokenPurchasesController.ConfirmCryptoRequest("0x1", null));

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Rechazar_un_alta_no_se_reenvia()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-SIGNUP", TokenPurchaseStatus.AwaitingCryptoConfirmation, CryptoPaymentPurpose.Signup);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);

        var r = await Build(db, factory.Object)
            .RejectCrypto("ORD-SIGNUP", new AdminTokenPurchasesController.RejectCryptoRequest("x"));

        r.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Un_id_con_barras_no_cambia_la_ruta_de_Billing()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "A/../B", TokenPurchaseStatus.AwaitingCryptoConfirmation);
        var (factory, captura) = Billing();

        await Build(db, factory.Object, "Bearer admin.jwt")
            .ConfirmCrypto("A/../B", new AdminTokenPurchasesController.ConfirmCryptoRequest("0x1", null));

        captura.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/crypto/A%2F..%2FB/confirm");
    }
}
