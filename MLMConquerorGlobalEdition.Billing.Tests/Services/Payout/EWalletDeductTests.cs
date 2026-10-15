using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

/// <summary>
/// El débito de eWallet como lo hace el legado (eWallet_AddCheckoutItems con AutoChargeAccount) y
/// cómo se lee la respuesta: código negativo es rechazo; todo lo que no sea una respuesta clara es
/// "no se sabe", nunca "falló" (falló permitiría reintentar y cobrar dos veces).
/// </summary>
public class EWalletDeductTests
{
    private sealed class Manejador(Func<HttpRequestMessage, string, HttpResponseMessage> f) : HttpMessageHandler
    {
        public string? Cuerpo;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Cuerpo = await r.Content!.ReadAsStringAsync(ct);
            return f(r, Cuerpo);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static EWalletClient Cliente(Manejador h)
    {
        AppDbContext db = TestDbContextFactory.Create();
        db.ApiCredentials.Add(new ApiCredential
        {
            ServiceKey = "EWallet", Environment = "Sandbox", BaseUrl = "https://ewallet.pruebas/api",
            ApiKeyEncrypted = "ENC:merchant-guid", SecretKeyEncrypted = "ENC:merchant-pass", IsActive = true,
            CreatedBy = "seed", CreationDate = DateTime.Now
        });
        db.SaveChanges();
        var crypto = new Mock<IEncryptionService>();
        crypto.Setup(c => c.Decrypt(It.IsAny<string>())).Returns<string>(s => s[4..]);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(EWalletClient.HttpClientName)).Returns(new HttpClient(h));
        return new EWalletClient(factory.Object, db, crypto.Object, NullLogger<EWalletClient>.Instance);
    }

    [Fact]
    public async Task Manda_el_cobro_como_el_legado()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK,
            """{"response":{"m_Code":0,"m_Text":""},"arrItemsResponse":[{"m_Code":0,"m_Text":""}]}"""));

        var d = await Cliente(h).DeductAsync("AMB-1", 99.99m, "purchase-1", "Token purchase TP1");

        d.Status.Should().Be(EWalletDeductionStatus.Accepted);
        using var json = JsonDocument.Parse(h.Cuerpo!);
        var root = json.RootElement;
        root.GetProperty("fn").GetString().Should().Be("eWallet_AddCheckoutItems");
        root.GetProperty("MerchantGUID").GetString().Should().Be("merchant-guid");
        root.GetProperty("UserName").GetString().Should().Be("AMB-1");
        root.GetProperty("AutoChargeAccount").GetString().Should().Be("true");
        var item = root.GetProperty("arrItems")[0];
        item.GetProperty("Amount").GetDecimal().Should().Be(99.99m);
        item.GetProperty("CurrencyCode").GetString().Should().Be("USD");
        item.GetProperty("MerchantReferenceID").GetString().Should().Be("purchase-1");
        item.GetProperty("IsSubscription").GetString().Should().Be("false");
        item.GetProperty("MustComplete").GetString().Should().Be("false");
    }

    [Fact]
    public async Task Un_item_con_codigo_negativo_es_rechazo()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK,
            """{"response":{"m_Code":0,"m_Text":""},"arrItemsResponse":[{"m_Code":-5,"m_Text":"INSUFFICIENT_FUNDS"}]}"""));

        var d = await Cliente(h).DeductAsync("AMB-1", 10m, "p", "x");

        d.Status.Should().Be(EWalletDeductionStatus.Rejected);
        d.Code.Should().Be("EWALLET_ITEM_-5");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "esto no es json")]
    public async Task Una_respuesta_que_no_se_entiende_es_desconocida(HttpStatusCode status, string body)
    {
        var d = await Cliente(new Manejador((_, _) => Json(status, body))).DeductAsync("AMB-1", 10m, "p", "x");

        d.Status.Should().Be(EWalletDeductionStatus.Unknown);
    }

    [Fact]
    public async Task Una_caida_de_red_es_desconocida()
    {
        var d = await Cliente(new Manejador((_, _) => throw new HttpRequestException("caida")))
            .DeductAsync("AMB-1", 10m, "p", "x");

        d.Status.Should().Be(EWalletDeductionStatus.Unknown);
    }
}
