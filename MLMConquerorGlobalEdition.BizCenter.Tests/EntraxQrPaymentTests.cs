using Bunit;
using Microsoft.AspNetCore.Components;
using MLMConquerorGlobalEdition.SharedComponents.Lite.Payments;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El QR de rublos por SBP del alta: lo pinta, cuenta hacia atrás, pregunta el estado cada pocos
/// segundos y, según lo que diga el servidor, avisa a la página o le ofrece un QR nuevo u otro método.
///
/// El tiempo es un <see cref="TimeProvider"/> manual: ninguna prueba espera de verdad ni depende de la
/// velocidad de la máquina. Cada "turno" espera a que el componente haya pedido su siguiente pausa y la
/// avanza.
/// </summary>
public class EntraxQrPaymentTests : BunitContext
{
    private static readonly TimeSpan Turno = EntraxQrPayment.MinPollInterval;

    /// <summary>Reloj que solo avanza cuando la prueba lo manda, con temporizadores manuales.</summary>
    private sealed class RelojManual : TimeProvider
    {
        private readonly object _lock = new();
        private readonly List<Temporizador> _activos = [];
        private long _ms;

        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() { lock (_lock) return _ms; }

        /// <summary>Pausas pedidas y aún sin cumplir.</summary>
        public int Pendientes { get { lock (_lock) return _activos.Count; } }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var t = new Temporizador(this, callback, state);
            t.Change(dueTime, period);
            return t;
        }

        public void Avanzar(TimeSpan tiempo)
        {
            List<Temporizador> vencidos;
            lock (_lock)
            {
                _ms += (long)tiempo.TotalMilliseconds;
                vencidos = _activos.Where(t => t.VenceEn <= _ms).ToList();
                foreach (var t in vencidos) _activos.Remove(t);
            }
            // Como un temporizador de verdad, el aviso sale por el pool y no por el hilo de la prueba: si no,
            // todo el bucle del componente correría dentro de Avanzar, tomando los bloqueos del renderizador.
            foreach (var t in vencidos) _ = Task.Run(t.Disparar);
        }

        private sealed class Temporizador(RelojManual reloj, TimerCallback callback, object? state) : ITimer
        {
            public long VenceEn;
            private bool _liberado;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (reloj._lock)
                {
                    // Como un temporizador de verdad: tras Dispose, Change no hace nada.
                    if (_liberado) return false;
                    reloj._activos.Remove(this);
                    if (dueTime == Timeout.InfiniteTimeSpan) return true;
                    VenceEn = reloj._ms + (long)dueTime.TotalMilliseconds;
                    reloj._activos.Add(this);
                }
                return true;
            }

            public void Disparar() => callback(state);
            public void Dispose() { lock (reloj._lock) { _liberado = true; reloj._activos.Remove(this); } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private readonly RelojManual _reloj = new();

    public EntraxQrPaymentTests()
    {
        // Las esperas son por condición, no por tiempo; el límite solo evita colgar la ejecución si algo
        // falla, y es holgado para que un equipo cargado no dé falsos rojos.
        DefaultWaitTimeout = TimeSpan.FromSeconds(30);
    }

    private static EntraxSignupPaymentDto Pago(string referencia = "ref-1", int segundos = 900) => new()
    {
        PaymentRef = referencia, QrImage = "data:image/png;base64,iVBORw0KGgo=", QrLink = "https://qr.nspk.ru/AS100",
        AmountUsd = 80m, AmountRub = 7640m, ExchangeRate = 95.5m, SecondsRemaining = segundos
    };

    private static Func<CancellationToken, Task<EntraxSignupStatusDto?>> Dice(string estado, int segundos = 0)
        => _ => Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado, SecondsRemaining = segundos });

    private IRenderedComponent<EntraxQrPayment> Pinta(
        Func<CancellationToken, Task<EntraxSignupStatusDto?>>? estado = null,
        EntraxSignupPaymentDto? pago = null,
        Action<ComponentParameterCollectionBuilder<EntraxQrPayment>>? mas = null)
        => Render<EntraxQrPayment>(p =>
        {
            p.Add(c => c.Payment, pago ?? Pago());
            p.Add(c => c.Time, _reloj);
            if (estado is not null) p.Add(c => c.CheckStatus, estado);
            mas?.Invoke(p);
        });

    /// <summary>Espera a que el componente esté dormido en su pausa y la cumple, avanzando <paramref name="tiempo"/>.</summary>
    private void Turno_(IRenderedComponent<EntraxQrPayment> cut, TimeSpan? tiempo = null)
    {
        // El bucle registra su pausa DESPUÉS de repintar, así que no hay un render que avise: se espera
        // a la condición, no a un tiempo.
        SpinWait.SpinUntil(() => _reloj.Pendientes > 0, TimeSpan.FromSeconds(10)).Should().BeTrue("el bucle debe pedir su siguiente pausa.");
        _reloj.Avanzar(tiempo ?? Turno);
    }

    private static string Estado(IRenderedComponent<EntraxQrPayment> cut) => cut.Find(".entrax-qr").GetAttribute("data-state")!;

    private static void EsperaEstado(IRenderedComponent<EntraxQrPayment> cut, string estado)
        => cut.WaitForAssertion(() => Estado(cut).Should().Be(estado));

    [Fact]
    public void Muestra_el_QR_el_enlace_seguro_el_importe_en_rublos_y_la_cuenta_atras()
    {
        var cut = Pinta();

        cut.Find("img.entrax-qr-image").GetAttribute("src").Should().Be("data:image/png;base64,iVBORw0KGgo=");
        var enlace = cut.Find("a.entrax-link");
        enlace.GetAttribute("href").Should().Be("https://qr.nspk.ru/AS100");
        enlace.GetAttribute("rel").Should().Contain("noopener").And.Contain("noreferrer");
        cut.Find(".entrax-amount").TextContent.Should().Contain("7,640.00").And.Contain("80.00");
        cut.Find(".entrax-countdown").TextContent.Should().Contain("15:00");
    }

    [Fact]
    public void La_cuenta_atras_baja_con_el_reloj_y_no_con_la_hora_del_navegador()
    {
        var cut = Pinta();

        Turno_(cut, TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => cut.Find(".entrax-countdown").TextContent.Should().Contain("14:59"));
    }

    [Fact]
    public void La_cuenta_atras_se_corrige_con_los_segundos_del_servidor()
    {
        var cut = Pinta(Dice(EntraxSignupStatuses.AwaitingPayment, 120));

        Turno_(cut);   // 5 s: toca preguntar

        cut.WaitForAssertion(() => cut.Find(".entrax-countdown").TextContent.Should().Contain("02:00"));
    }

    [Fact]
    public void Sin_imagen_ni_enlace_no_pinta_nada_roto()
    {
        var pago = Pago();
        pago.QrImage = null;
        pago.QrLink  = null;

        var cut = Pinta(pago: pago);

        cut.FindAll("img").Should().BeEmpty();
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void Una_imagen_o_un_enlace_con_otro_esquema_no_llegan_al_DOM()
    {
        var pago = Pago();
        pago.QrImage = "javascript:alert(1)";
        pago.QrLink  = "http://qr.nspk.ru/AS100";

        var cut = Pinta(pago: pago);

        cut.FindAll("img").Should().BeEmpty();
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void Cuando_el_servidor_dice_pagado_avisa_a_la_pagina_y_lo_dice()
    {
        var avisos = 0;
        var cut = Pinta(Dice(EntraxSignupStatuses.Paid),
            mas: p => p.Add(c => c.OnPaid, EventCallback.Factory.Create(this, () => avisos++)));

        Turno_(cut);

        EsperaEstado(cut, EntraxSignupStatuses.Paid);
        cut.WaitForAssertion(() => avisos.Should().Be(1));
        cut.Markup.Should().Contain("Payment received");
    }

    [Fact]
    public void Si_el_alta_ya_se_completo_por_otro_medio_oculta_el_QR_y_manda_a_iniciar_sesion()
    {
        var avisos = 0;
        var cut = Pinta(Dice(EntraxSignupStatuses.Superseded),
            mas: p => p.Add(c => c.OnPaid, EventCallback.Factory.Create(this, () => avisos++)));

        Turno_(cut);

        EsperaEstado(cut, EntraxSignupStatuses.Superseded);
        cut.Markup.Should().Contain("already complete");
        cut.FindAll("img").Should().BeEmpty("un QR de un alta ya activada no puede parecer cobrable.");
        cut.FindAll("a").Should().BeEmpty();
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
        cut.WaitForAssertion(() => avisos.Should().Be(1));
    }

    [Theory]
    [InlineData(EntraxSignupStatuses.Expired)]
    [InlineData(EntraxSignupStatuses.Failed)]
    public void Caducado_o_fallido_ofrece_un_QR_nuevo_u_otro_metodo(string estado)
    {
        var reintentos = 0;
        var otro = 0;
        var cut = Pinta(Dice(estado), mas: p => p
            .Add(c => c.OnRetry, EventCallback.Factory.Create(this, () => reintentos++))
            .Add(c => c.OnChooseAnother, EventCallback.Factory.Create(this, () => otro++)));

        Turno_(cut);
        EsperaEstado(cut, estado);

        // Tras un estado final el componente no vuelve a pintar: el botón que se encuentra es el definitivo.
        cut.Find(".entrax-new-qr").Click();
        cut.Find(".entrax-other").Click();

        reintentos.Should().Be(1);
        otro.Should().Be(1);
        cut.FindAll("img").Should().BeEmpty("un QR caducado no se vuelve a enseñar.");
    }

    [Fact]
    public void Mientras_la_pagina_pide_otro_QR_los_botones_estan_deshabilitados()
    {
        var cut = Pinta(Dice(EntraxSignupStatuses.Expired), mas: p => p.Add(c => c.Busy, true));

        Turno_(cut);
        EsperaEstado(cut, EntraxSignupStatuses.Expired);

        cut.Find(".entrax-new-qr").HasAttribute("disabled").Should().BeTrue();
        cut.Find(".entrax-other").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void A_revisar_no_ofrece_pagar_otra_vez()
    {
        var cut = Pinta(Dice(EntraxSignupStatuses.ToReview));

        Turno_(cut);
        EsperaEstado(cut, EntraxSignupStatuses.ToReview);

        cut.Markup.Should().Contain("need to review it");
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
        cut.FindAll("img").Should().BeEmpty();
    }

    [Theory]
    [InlineData(EntraxSignupStatuses.Paid)]
    [InlineData(EntraxSignupStatuses.Superseded)]
    [InlineData(EntraxSignupStatuses.ToReview)]
    [InlineData(EntraxSignupStatuses.Expired)]
    [InlineData(EntraxSignupStatuses.Failed)]
    public void En_un_estado_final_deja_de_preguntar(string estado)
    {
        var llamadas = 0;
        var cut = Pinta(_ =>
        {
            Interlocked.Increment(ref llamadas);
            return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado });
        });

        Turno_(cut);
        EsperaEstado(cut, estado);

        // Sin pausas pendientes el bucle ya terminó: no hay un turno siguiente que pueda preguntar.
        SpinWait.SpinUntil(() => _reloj.Pendientes == 0, TimeSpan.FromSeconds(10)).Should().BeTrue();
        llamadas.Should().Be(1);
    }

    [Fact]
    public void Pregunta_como_mucho_cada_5_segundos_aunque_se_pida_menos()
    {
        var llamadas = 0;
        var cut = Pinta(_ =>
        {
            Interlocked.Increment(ref llamadas);
            return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto
                { Status = EntraxSignupStatuses.AwaitingPayment, SecondsRemaining = 800 });
        }, mas: p => p.Add(c => c.PollInterval, TimeSpan.FromMilliseconds(10)));

        for (var i = 0; i < 4; i++) Turno_(cut, TimeSpan.FromSeconds(1));
        llamadas.Should().Be(0, "en 4 s no toca preguntar: el suelo es de 5 s.");

        Turno_(cut, TimeSpan.FromSeconds(1));
        cut.WaitForAssertion(() => llamadas.Should().Be(1));
    }

    [Fact]
    public async Task Al_destruir_el_componente_cancela_la_consulta_en_curso()
    {
        CancellationToken token = default;
        var llamadas = 0;
        var cut = Pinta(ct =>
        {
            token = ct;
            Interlocked.Increment(ref llamadas);
            return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto
                { Status = EntraxSignupStatuses.AwaitingPayment, SecondsRemaining = 500 });
        });
        Turno_(cut);
        cut.WaitForAssertion(() => llamadas.Should().Be(1));
        token.IsCancellationRequested.Should().BeFalse();

        await DisposeComponentsAsync();

        SpinWait.SpinUntil(() => token.IsCancellationRequested, TimeSpan.FromSeconds(10))
            .Should().BeTrue("el bucle y la consulta comparten el token que se cancela al destruir.");
    }

    [Fact]
    public void Un_fallo_de_red_no_cambia_nada_y_se_sigue_preguntando()
    {
        var llamadas = 0;
        var cut = Pinta(_ =>
        {
            Interlocked.Increment(ref llamadas);
            return Task.FromException<EntraxSignupStatusDto?>(new HttpRequestException("caido"));
        });

        Turno_(cut);
        cut.WaitForAssertion(() => llamadas.Should().Be(1));
        Turno_(cut);
        cut.WaitForAssertion(() => llamadas.Should().Be(2));

        Estado(cut).Should().Be(EntraxSignupStatuses.AwaitingPayment);
        cut.Markup.Should().Contain("Waiting for your payment");
    }

    [Fact]
    public void Un_estado_desconocido_se_ignora()
    {
        var llamadas = 0;
        var cut = Pinta(_ =>
        {
            Interlocked.Increment(ref llamadas);
            return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = "Inventado" });
        });

        Turno_(cut);
        cut.WaitForAssertion(() => llamadas.Should().Be(1));

        Estado(cut).Should().Be(EntraxSignupStatuses.AwaitingPayment);
        cut.FindAll("img.entrax-qr-image").Should().HaveCount(1);
    }

    [Fact]
    public void Con_la_cuenta_a_cero_y_el_pago_sin_cerrar_oculta_el_QR_y_sigue_preguntando()
    {
        var llamadas = 0;
        var cut = Pinta(_ =>
        {
            Interlocked.Increment(ref llamadas);
            return Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto
                { Status = EntraxSignupStatuses.AwaitingPayment, SecondsRemaining = 0 });
        }, pago: Pago(segundos: 3));

        Turno_(cut);   // 5 s: la cuenta (3 s) llega a cero y toca preguntar

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("do not pay it"));
        cut.FindAll("img").Should().BeEmpty("un QR caducado no se enseña aunque el servidor no lo haya cerrado.");
        cut.FindAll("a").Should().BeEmpty();

        Turno_(cut);
        cut.WaitForAssertion(() => llamadas.Should().Be(2));
        Estado(cut).Should().Be(EntraxSignupStatuses.AwaitingPayment);
    }

    [Fact]
    public void Un_QR_nuevo_vuelve_a_esperar()
    {
        var estado = EntraxSignupStatuses.Expired;
        var cut = Pinta(_ => Task.FromResult<EntraxSignupStatusDto?>(new EntraxSignupStatusDto { Status = estado }));
        Turno_(cut);
        EsperaEstado(cut, EntraxSignupStatuses.Expired);

        estado = EntraxSignupStatuses.AwaitingPayment;
        cut.Render(p => p.Add(c => c.Payment, Pago("ref-2")));

        cut.WaitForAssertion(() => cut.Find("img.entrax-qr-image"));
        cut.FindAll(".entrax-new-qr").Should().BeEmpty();
        EsperaEstado(cut, EntraxSignupStatuses.AwaitingPayment);
    }
}
