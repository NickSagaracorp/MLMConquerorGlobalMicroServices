using Bunit;
using Microsoft.AspNetCore.Components;
using MLMConquerorGlobalEdition.SharedComponents.Lite.Payments;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El QR de rublos por SBP del alta: lo pinta, cuenta hacia atrás, pregunta el estado cada pocos
/// segundos y, según lo que diga el servidor, avisa a la página o le ofrece un QR nuevo u otro método.
/// </summary>
public class EntraxQrPaymentTests : BunitContext
{
    private static readonly TimeSpan Rapido = TimeSpan.FromMilliseconds(10);

    private static EntraxSignupPaymentDto Pago(string referencia = "ref-1") => new()
    {
        PaymentRef = referencia, QrImage = "data:image/png;base64,iVBORw0KGgo=", QrLink = "https://qr.nspk.ru/AS100",
        AmountUsd = 80m, AmountRub = 7640m, ExchangeRate = 95.5m, SecondsRemaining = 900
    };

    private static Func<CancellationToken, Task<EntraxSignupStatusDto?>> Dice(string estado)
        => _ => Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado });

    [Fact]
    public void Muestra_el_QR_el_enlace_seguro_el_importe_en_rublos_y_la_cuenta_atras()
    {
        var cut = Render<EntraxQrPayment>(p => p.Add(c => c.Payment, Pago()));

        cut.Find("img.entrax-qr-image").GetAttribute("src").Should().Be("data:image/png;base64,iVBORw0KGgo=");
        var enlace = cut.Find("a.entrax-link");
        enlace.GetAttribute("href").Should().Be("https://qr.nspk.ru/AS100");
        enlace.GetAttribute("rel").Should().Contain("noopener").And.Contain("noreferrer");
        cut.Find(".entrax-amount").TextContent.Should().Contain("7,640.00").And.Contain("80.00");
        cut.Find(".entrax-countdown").TextContent.Should().MatchRegex("1[45]:[0-5][0-9]");
    }

    [Fact]
    public void Sin_imagen_ni_enlace_no_pinta_nada_roto()
    {
        var pago = Pago();
        pago.QrImage = null;
        pago.QrLink  = null;

        var cut = Render<EntraxQrPayment>(p => p.Add(c => c.Payment, pago));

        cut.FindAll("img").Should().BeEmpty();
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void Una_imagen_o_un_enlace_con_otro_esquema_no_llegan_al_DOM()
    {
        var pago = Pago();
        pago.QrImage = "javascript:alert(1)";
        pago.QrLink  = "http://qr.nspk.ru/AS100";

        var cut = Render<EntraxQrPayment>(p => p.Add(c => c.Payment, pago));

        cut.FindAll("img").Should().BeEmpty();
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void Cuando_el_servidor_dice_pagado_avisa_a_la_pagina_y_lo_dice()
    {
        var pagado = false;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(EntraxSignupStatuses.Paid))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido)
            .Add(c => c.OnPaid, EventCallback.Factory.Create(this, () => pagado = true)));

        cut.WaitForAssertion(() => pagado.Should().BeTrue(), TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Payment received"));
    }

    [Fact]
    public void Si_el_alta_ya_se_completo_por_otro_medio_oculta_el_QR_y_manda_a_iniciar_sesion()
    {
        var avisos = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(EntraxSignupStatuses.Superseded))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido)
            .Add(c => c.OnPaid, EventCallback.Factory.Create(this, () => avisos++)));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("already complete"), TimeSpan.FromSeconds(3));
        cut.FindAll("img").Should().BeEmpty("un QR de un alta ya activada no puede parecer cobrable.");
        cut.FindAll("a").Should().BeEmpty();
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
        avisos.Should().Be(1);
    }

    [Theory]
    [InlineData(EntraxSignupStatuses.Expired)]
    [InlineData(EntraxSignupStatuses.Failed)]
    public void Caducado_o_fallido_ofrece_un_QR_nuevo_u_otro_metodo(string estado)
    {
        var reintentos = 0;
        var otro = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(estado))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido)
            .Add(c => c.OnRetry, EventCallback.Factory.Create(this, () => reintentos++))
            .Add(c => c.OnChooseAnother, EventCallback.Factory.Create(this, () => otro++)));

        // Entre localizar el botón y pulsarlo puede haber un render que cambie su manejador; si pasa, la
        // pulsación falla sin contar y se repite.
        cut.WaitForAssertion(() => cut.Find(".entrax-new-qr").Click(), TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(() => cut.Find(".entrax-other").Click(), TimeSpan.FromSeconds(3));

        reintentos.Should().Be(1);
        otro.Should().Be(1);
        cut.FindAll("img").Should().BeEmpty("un QR caducado no se vuelve a enseñar.");
    }

    [Fact]
    public void Mientras_la_pagina_pide_otro_QR_los_botones_estan_deshabilitados()
    {
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(EntraxSignupStatuses.Expired))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido)
            .Add(c => c.Busy, true));

        cut.WaitForAssertion(() => cut.Find(".entrax-new-qr"), TimeSpan.FromSeconds(3));
        cut.Find(".entrax-new-qr").HasAttribute("disabled").Should().BeTrue();
        cut.Find(".entrax-other").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void A_revisar_no_ofrece_pagar_otra_vez()
    {
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, Dice(EntraxSignupStatuses.ToReview))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("need to review it"), TimeSpan.FromSeconds(3));
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
        cut.FindAll("img").Should().BeEmpty();
    }

    [Theory]
    [InlineData(EntraxSignupStatuses.Paid)]
    [InlineData(EntraxSignupStatuses.Superseded)]
    [InlineData(EntraxSignupStatuses.ToReview)]
    [InlineData(EntraxSignupStatuses.Expired)]
    [InlineData(EntraxSignupStatuses.Failed)]
    public async Task En_un_estado_final_deja_de_preguntar(string estado)
    {
        var llamadas = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, _ =>
            {
                Interlocked.Increment(ref llamadas);
                return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado });
            })
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));

        cut.WaitForAssertion(() => llamadas.Should().BeGreaterThan(0), TimeSpan.FromSeconds(3));
        await Task.Delay(150);
        var antes = Volatile.Read(ref llamadas);
        await Task.Delay(150);

        Volatile.Read(ref llamadas).Should().Be(antes, "con un estado final no se vuelve a preguntar al servidor.");
        llamadas.Should().Be(1);
    }

    [Fact]
    public async Task Al_destruir_el_componente_deja_de_preguntar()
    {
        var llamadas = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, _ =>
            {
                Interlocked.Increment(ref llamadas);
                return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto
                    { Status = EntraxSignupStatuses.AwaitingPayment, SecondsRemaining = 500 });
            })
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));
        cut.WaitForAssertion(() => llamadas.Should().BeGreaterThan(1), TimeSpan.FromSeconds(3));

        await DisposeComponentsAsync();
        await Task.Delay(100);
        var antes = Volatile.Read(ref llamadas);
        await Task.Delay(150);

        Volatile.Read(ref llamadas).Should().Be(antes);
    }

    [Fact]
    public void Un_fallo_de_red_no_cambia_nada_y_se_sigue_preguntando()
    {
        var llamadas = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, _ =>
            {
                Interlocked.Increment(ref llamadas);
                return Task.FromException<EntraxSignupStatusDto?>(new HttpRequestException("caido"));
            })
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));

        cut.WaitForAssertion(() => llamadas.Should().BeGreaterThan(1), TimeSpan.FromSeconds(3));
        cut.Markup.Should().Contain("Waiting for your payment");
    }

    [Fact]
    public void Un_estado_desconocido_se_ignora()
    {
        var llamadas = 0;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, _ =>
            {
                Interlocked.Increment(ref llamadas);
                return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = "Inventado" });
            })
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));

        cut.WaitForAssertion(() => llamadas.Should().BeGreaterThan(1), TimeSpan.FromSeconds(3));
        cut.FindAll("img.entrax-qr-image").Should().HaveCount(1);
    }

    [Fact]
    public void La_cuenta_atras_se_corrige_con_los_segundos_del_servidor()
    {
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago())
            .Add(c => c.CheckStatus, _ => Task.FromResult<EntraxSignupStatusDto?>(
                new EntraxSignupStatusDto { Status = EntraxSignupStatuses.AwaitingPayment, SecondsRemaining = 120 }))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));

        cut.WaitForAssertion(
            () => cut.Find(".entrax-countdown").TextContent.Should().MatchRegex("0[12]:[0-5][0-9]"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Un_QR_nuevo_vuelve_a_esperar()
    {
        var estado = EntraxSignupStatuses.Expired;
        var cut = Render<EntraxQrPayment>(p => p
            .Add(c => c.Payment, Pago("ref-1"))
            .Add(c => c.CheckStatus, _ => Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado }))
            .Add(c => c.PollInterval, Rapido)
            .Add(c => c.TickInterval, Rapido));
        cut.WaitForAssertion(() => cut.Find(".entrax-new-qr"), TimeSpan.FromSeconds(3));

        estado = EntraxSignupStatuses.AwaitingPayment;
        cut.Render(p => p.Add(c => c.Payment, Pago("ref-2")));

        cut.WaitForAssertion(() => cut.Find("img.entrax-qr-image"));
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
    }
}
