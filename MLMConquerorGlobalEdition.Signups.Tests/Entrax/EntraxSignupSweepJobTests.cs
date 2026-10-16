using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El barrido liquida lo que el webhook y la pantalla no liquidaron: QR esperando y QR caducados
/// hace poco (un pago tardío). Pocos por pasada, los menos consultados primero.
/// </summary>
public class EntraxSignupSweepJobTests
{
    private static readonly DateTime Ahora = EscenarioEntrax.Ahora;

    private static async Task<AppDbContext> AltaAsync()
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        return db;
    }

    private static Task<EntraxPayment> Pago(AppDbContext db, int intento, EntraxPaymentStatus estado,
        DateTime? caduca = null, DateTime? consultado = null)
        => EscenarioEntrax.SembrarPagoAsync(db, estado, paymentId: Guid.NewGuid().ToString(), intento: intento,
            caduca: caduca, consultado: consultado);

    /// <summary>La liquidación se resuelve en un scope propio por pago; aquí el scope da el doble.</summary>
    private static EntraxSignupSweepJob Job(AppDbContext db, Mock<IEntraxSignupSettlement> liquidacion,
        Action<EntraxOptions>? opciones = null, List<IServiceProvider>? scopes = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(sp =>
        {
            scopes?.Add(sp);
            return liquidacion.Object;
        });
        var provider = services.BuildServiceProvider();
        return new(db, provider.GetRequiredService<IServiceScopeFactory>(), EscenarioEntrax.Opciones(opciones),
                   EscenarioEntrax.Reloj().Object, NullLogger<EntraxSignupSweepJob>.Instance);
    }

    [Fact]
    public async Task Liquida_los_que_esperan_y_los_caducados_recientes_y_nada_mas()
    {
        await using var db = await AltaAsync();
        var esperando     = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        var caducoHacePoco = await Pago(db, 2, EntraxPaymentStatus.Expired, caduca: Ahora.AddMinutes(-30));
        await Pago(db, 3, EntraxPaymentStatus.Expired, caduca: Ahora.AddHours(-3));
        await Pago(db, 4, EntraxPaymentStatus.Paid);
        await Pago(db, 5, EntraxPaymentStatus.ToReview);
        await Pago(db, 6, EntraxPaymentStatus.Failed);
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        await Job(db, liquidacion).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(esperando.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.Verify(s => s.SettleAsync(caducoHacePoco.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Toma_como_mucho_el_lote_empezando_por_los_menos_consultados()
    {
        await using var db = await AltaAsync();
        var nunca      = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        await Pago(db, 2, EntraxPaymentStatus.AwaitingPayment, consultado: Ahora.AddMinutes(-1));
        var haceDiez   = await Pago(db, 3, EntraxPaymentStatus.AwaitingPayment, consultado: Ahora.AddMinutes(-10));
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        await Job(db, liquidacion, o => o.SweepBatchSize = 2).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(nunca.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.Verify(s => s.SettleAsync(haceDiez.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_fallo_en_uno_no_corta_los_demas()
    {
        await using var db = await AltaAsync();
        var primero = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        var segundo = await Pago(db, 2, EntraxPaymentStatus.AwaitingPayment, consultado: Ahora);
        var liquidacion = new Mock<IEntraxSignupSettlement>();
        liquidacion.Setup(s => s.SettleAsync(primero.Id, It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("boom"));

        await Job(db, liquidacion).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(segundo.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Corre_aunque_Entrax_este_apagado_para_no_dejar_pagos_en_vuelo()
    {
        await using var db = await AltaAsync();
        var esperando = await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        await Job(db, liquidacion, o => o.Enabled = false).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(esperando.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cada_pago_se_liquida_en_su_propio_scope_para_no_arrastrar_cambios_de_otro()
    {
        await using var db = await AltaAsync();
        await Pago(db, 1, EntraxPaymentStatus.AwaitingPayment);
        await Pago(db, 2, EntraxPaymentStatus.AwaitingPayment, consultado: Ahora);
        var liquidacion = new Mock<IEntraxSignupSettlement>();
        var scopes = new List<IServiceProvider>();

        await Job(db, liquidacion, scopes: scopes).ExecuteAsync();

        scopes.Should().HaveCount(2).And.OnlyHaveUniqueItems(
            "si una liquidación revienta a medias, lo que dejó en su DbContext no puede guardarse con la siguiente.");
    }
}
