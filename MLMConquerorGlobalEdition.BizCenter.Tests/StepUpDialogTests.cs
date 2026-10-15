using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El modal del código de step-up. Lo que se protege: que devuelva el permiso solo con un canje
/// correcto, que cancelar (o destruir el componente) nunca deje colgado a quien espera, y que cada
/// código de error de la API salga como su clave de recurso y no como texto crudo.
/// </summary>
public class StepUpDialogTests : BunitContext
{
    /// <summary>Devuelve la CLAVE (y los argumentos) en vez del texto, para no atar las pruebas al .resx.</summary>
    internal sealed class LocalizadorDeClaves : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, arguments.Length == 0 ? name : name + "|" + string.Join("|", arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class ClienteFalso : IStepUpClient
    {
        public StepUpOutcome<StepUpChallenge> Reto { get; set; } = new(true, null,
            new StepUpChallenge("reto", "Email", "n***@correo.com", DateTime.Now.AddMinutes(10)));

        public StepUpOutcome<string> Canje { get; set; } = new(true, null, "permiso-123");

        /// <summary>Si se asigna, el canje no termina hasta que se complete: simula una petición en vuelo.</summary>
        public TaskCompletionSource<StepUpOutcome<string>>? CanjeEnVuelo { get; set; }

        public bool CanjeLanza { get; set; }
        public int Retos { get; private set; }
        public string? CodigoEnviado { get; private set; }

        public Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string operationKey, CancellationToken ct = default)
        {
            Retos++;
            return Task.FromResult(Reto);
        }

        public Task<StepUpOutcome<string>> VerifyAsync(
            string operationKey, string challengeToken, string code, CancellationToken ct = default)
        {
            CodigoEnviado = code;
            if (CanjeLanza) throw new HttpRequestException("caído");
            return CanjeEnVuelo?.Task ?? Task.FromResult(Canje);
        }
    }

    private readonly ClienteFalso _cliente = new();

    public StepUpDialogTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new LocalizadorDeClaves());
        Services.AddSingleton<IStepUpClient>(_cliente);
    }

    private (IRenderedComponent<StepUpDialog> Dialogo, Task<string?> Espera) Abrir()
    {
        var dialogo = Render<StepUpDialog>();
        Task<string?> espera = null!;
        dialogo.InvokeAsync(() => { espera = dialogo.Instance.RequireAsync("member.payout-method.change"); });
        dialogo.WaitForAssertion(() => dialogo.Find(".stepup-target"));
        return (dialogo, espera);
    }

    private static void Teclear(IRenderedComponent<StepUpDialog> d, string codigo) =>
        d.Find("#stepup-code").Input(codigo);

    [Fact]
    public void Tras_el_reto_muestra_el_destino_enmascarado()
    {
        var (dialogo, _) = Abrir();

        dialogo.Find(".stepup-target").TextContent.Should().Contain("StepUp.SentTo").And.Contain("n***@correo.com");
    }

    [Fact]
    public async Task Con_el_codigo_correcto_completa_con_el_permiso_y_cierra()
    {
        var (dialogo, espera) = Abrir();

        Teclear(dialogo, " 123456 ");
        await dialogo.Find(".stepup-confirm").ClickAsync(new());

        (await espera).Should().Be("permiso-123");
        _cliente.CodigoEnviado.Should().Be("123456", "el código se recorta antes de enviarlo");
        dialogo.FindAll(".modal").Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelar_completa_con_null_y_cierra()
    {
        var (dialogo, espera) = Abrir();

        await dialogo.Find(".stepup-cancel").ClickAsync(new());

        (await espera).Should().BeNull();
        dialogo.FindAll(".modal").Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelar_con_el_canje_en_vuelo_no_deja_colgada_la_espera_ni_la_revive()
    {
        _cliente.CanjeEnVuelo = new TaskCompletionSource<StepUpOutcome<string>>();
        var (dialogo, espera) = Abrir();
        Teclear(dialogo, "123456");
        _ = dialogo.Find(".stepup-confirm").ClickAsync(new());

        await dialogo.Find(".stepup-cancel").ClickAsync(new());
        (await espera).Should().BeNull();

        // La respuesta tardía de una sesión cancelada no puede reabrir ni completar nada.
        _cliente.CanjeEnVuelo.SetResult(new(true, null, "tarde"));
        await Task.Delay(100); // deja correr la continuación del canje
        dialogo.FindAll(".modal").Should().BeEmpty();
        (await espera).Should().BeNull("la espera ya estaba resuelta y no se re-completa con el permiso tardío");
    }

    [Fact]
    public async Task Destruir_el_componente_con_la_espera_viva_la_completa_con_null()
    {
        var (dialogo, espera) = Abrir();

        await DisposeComponentsAsync();

        (await espera).Should().BeNull();
    }

    [Fact]
    public void El_dialogo_va_por_encima_de_cualquier_modal_de_Bootstrap()
    {
        var (dialogo, _) = Abrir();

        // Se abre desde otros modales (1055): con menos z-index quedaría detrás de su telón.
        dialogo.Find(".modal").GetAttribute("style").Should().Contain("z-index:1080");
    }

    [Fact]
    public async Task Reabrir_con_una_espera_viva_resuelve_la_vieja_con_null()
    {
        var (dialogo, vieja) = Abrir();

        Task<string?> nueva = null!;
        await dialogo.InvokeAsync(() => { nueva = dialogo.Instance.RequireAsync("member.payout-method.change"); });

        (await vieja).Should().BeNull();
        nueva.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Si_el_cliente_lanza_el_dialogo_no_se_queda_congelado()
    {
        _cliente.CanjeLanza = true;
        var (dialogo, espera) = Abrir();
        Teclear(dialogo, "123456");

        await dialogo.Find(".stepup-confirm").ClickAsync(new());

        dialogo.Find(".stepup-error").TextContent.Should().Be("TwoFactor.Error.Generic");
        dialogo.Find("#stepup-code").HasAttribute("disabled").Should().BeFalse("_busy se resetea");
        espera.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Un_codigo_de_error_se_muestra_como_su_clave()
    {
        _cliente.Canje = new(false, "CODE_INVALID", null);
        var (dialogo, espera) = Abrir();

        Teclear(dialogo, "000000");
        await dialogo.Find(".stepup-confirm").ClickAsync(new());

        dialogo.Find(".stepup-error").TextContent.Should().Be("TwoFactor.Error.CodeInvalid");
        espera.IsCompleted.Should().BeFalse("con un código malo el modal sigue abierto para reintentar");
    }

    [Fact]
    public async Task VERIFICATION_UNAVAILABLE_tiene_su_propio_texto()
    {
        _cliente.Canje = new(false, "VERIFICATION_UNAVAILABLE", null);
        var (dialogo, _) = Abrir();

        Teclear(dialogo, "000000");
        await dialogo.Find(".stepup-confirm").ClickAsync(new());

        dialogo.Find(".stepup-error").TextContent.Should().Be("TwoFactor.Error.VerificationUnavailable");
    }

    [Fact]
    public void La_espera_por_cambio_de_factor_muestra_la_fecha_del_mensaje()
    {
        _cliente.Reto = new(false, "STEP_UP_COOLDOWN", null,
            "Un factor cambió hace poco; disponible a partir de 2026-10-16T09:30:00.");

        var dialogo = Render<StepUpDialog>();
        dialogo.InvokeAsync(() => { _ = dialogo.Instance.RequireAsync("member.payout-method.change"); });

        dialogo.WaitForAssertion(() =>
            dialogo.Find(".stepup-error").TextContent.Should().Be(
                "StepUp.Cooldown|" + new DateTime(2026, 10, 16, 9, 30, 0).ToString("f", CultureInfo.CurrentCulture)));
        dialogo.FindAll("#stepup-code").Should().BeEmpty("sin reto no hay dónde teclear");
    }

    [Fact]
    public void La_espera_sin_fecha_cae_al_texto_generico()
    {
        _cliente.Reto = new(false, "STEP_UP_COOLDOWN", null, "sin fecha");

        var dialogo = Render<StepUpDialog>();
        dialogo.InvokeAsync(() => { _ = dialogo.Instance.RequireAsync("member.payout-method.change"); });

        dialogo.WaitForAssertion(() =>
            dialogo.Find(".stepup-error").TextContent.Should().Be("StepUp.CooldownGeneric"));
    }

    [Fact]
    public async Task Al_reabrir_el_estado_arranca_limpio()
    {
        _cliente.Canje = new(false, "CODE_INVALID", null);
        var (dialogo, _) = Abrir();
        Teclear(dialogo, "000000");
        await dialogo.Find(".stepup-confirm").ClickAsync(new());
        dialogo.FindAll(".stepup-error").Should().HaveCount(1);
        await dialogo.Find(".stepup-cancel").ClickAsync(new());

        await dialogo.InvokeAsync(() => { _ = dialogo.Instance.RequireAsync("member.payout-method.change"); });

        dialogo.WaitForAssertion(() => dialogo.Find(".stepup-target"));
        dialogo.FindAll(".stepup-error").Should().BeEmpty();
        dialogo.Find("#stepup-code").GetAttribute("value").Should().BeNullOrEmpty();
    }
}

/// <summary>Un anfitrión que no registra el cliente de step-up: la persona tiene que ver por qué no avanza.</summary>
public class StepUpDialogSinClienteTests : BunitContext
{
    public StepUpDialogSinClienteTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());

    [Fact]
    public async Task Sin_cliente_avisa_en_vez_de_no_hacer_nada_y_cancelar_devuelve_null()
    {
        var dialogo = Render<StepUpDialog>();
        Task<string?> espera = null!;
        await dialogo.InvokeAsync(() => { espera = dialogo.Instance.RequireAsync("member.payout-method.change"); });

        dialogo.Find(".stepup-error").TextContent.Should().Be("StepUp.Unavailable");
        dialogo.Find(".stepup-resend").HasAttribute("disabled").Should().BeTrue();
        dialogo.FindAll("#stepup-code").Should().BeEmpty();

        await dialogo.Find(".stepup-cancel").ClickAsync(new());
        (await espera).Should().BeNull();
    }
}
