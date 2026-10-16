using Bunit;
using Microsoft.AspNetCore.Components;
using MLMConquerorGlobalEdition.SharedComponents.Lite.Payments;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El selector de moneda y dirección de depósito que comparten el alta (Signup, MemberJoin) y la
/// compra de tokens. Sin direcciones no se ofrece cripto; con ellas se ve la dirección de la moneda
/// elegida y el texto que pone cada pantalla.
/// </summary>
public class CryptoDepositPickerTests : BunitContext
{
    private static readonly List<CryptoDepositAddressDto> Direcciones =
    [
        new() { Currency = "BTC",  Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh" },
        new() { Currency = "USDT", Address = "TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5", Network = "TRC-20" }
    ];

    [Fact]
    public void Sin_direcciones_dice_que_no_esta_disponible()
    {
        var cut = Render<CryptoDepositPicker>(p => p
            .Add(c => c.Addresses, new List<CryptoDepositAddressDto>())
            .AddChildContent("<p id='texto'>debajo</p>"));

        cut.Markup.Should().Contain("Crypto payments aren't available right now");
        cut.FindAll("select").Should().BeEmpty();
        cut.FindAll("#texto").Should().BeEmpty("sin cripto no hay nada que explicar debajo.");
        CryptoDepositPicker.Find(new List<CryptoDepositAddressDto>(), "BTC").Should().BeNull(
            "la página deshabilita el botón de pagar con esto.");
    }

    [Fact]
    public void Muestra_la_direccion_de_la_moneda_elegida_y_el_texto_de_la_pantalla()
    {
        var cut = Render<CryptoDepositPicker>(p => p
            .Add(c => c.Addresses, Direcciones)
            .Add(c => c.Currency, "USDT")
            .Add(c => c.InputId, "signup-crypto")
            .AddChildContent("<p id='texto'>debajo</p>"));

        cut.Find("select").Id.Should().Be("signup-crypto");
        cut.Find("label").GetAttribute("for").Should().Be("signup-crypto");
        cut.FindAll("option").Select(o => o.GetAttribute("value")).Should().Equal("BTC", "USDT");
        cut.Markup.Should().Contain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5").And.Contain("Network: TRC-20");
        cut.Find("#texto").TextContent.Should().Be("debajo");
    }

    [Fact]
    public void Cambiar_de_moneda_avisa_al_padre()
    {
        string? elegida = "BTC";
        var cut = Render<CryptoDepositPicker>(p => p
            .Add(c => c.Addresses, Direcciones)
            .Add(c => c.Currency, elegida)
            .Add(c => c.CurrencyChanged, EventCallback.Factory.Create<string?>(this, v => elegida = v)));

        cut.Find("select").Change("USDT");

        elegida.Should().Be("USDT");
        cut.Markup.Should().Contain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5");
    }

    [Fact]
    public void Por_defecto_habla_ingles_y_ensena_la_direccion_como_en_el_alta()
    {
        var cut = Render<CryptoDepositPicker>(p => p
            .Add(c => c.Addresses, Direcciones)
            .Add(c => c.Currency, "USDT"));

        cut.Find("label").TextContent.Should().Be("Currency");
        cut.Markup.Should().Contain("Send your payment in <strong>USDT</strong> to:")
            .And.Contain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5");
    }

    [Fact]
    public void Los_textos_llegan_por_parametro()
    {
        var sinDirecciones = Render<CryptoDepositPicker>(p => p
            .Add(c => c.Addresses, new List<CryptoDepositAddressDto>())
            .Add(c => c.UnavailableText, "Cripto no disponible"));
        sinDirecciones.Markup.Should().Contain("Cripto no disponible").And.NotContain("Crypto payments");

        var cut = Render<CryptoDepositPicker>(p => p
            .Add(c => c.Addresses, Direcciones)
            .Add(c => c.Currency, "USDT")
            .Add(c => c.CurrencyLabel, "Moneda")
            .Add(c => c.SendToText, "Envía tu pago en {0} a:")
            .Add(c => c.NetworkText, "Red: {0}"));

        cut.Find("label").TextContent.Should().Be("Moneda");
        cut.Markup.Should().Contain("Envía tu pago en <strong>USDT</strong> a:").And.Contain("Red: TRC-20");
    }

    [Fact]
    public void Sin_ShowAddress_solo_se_elige_la_moneda()
    {
        var cut = Render<CryptoDepositPicker>(p => p
            .Add(c => c.Addresses, Direcciones)
            .Add(c => c.Currency, "USDT")
            .Add(c => c.ShowAddress, false)
            .AddChildContent("<p id='texto'>debajo</p>"));

        cut.FindAll("select").Should().HaveCount(1);
        cut.Markup.Should().NotContain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5", "la dirección buena es la que devuelve Billing al crear la compra")
            .And.NotContain("TRC-20");
        cut.Find("#texto").TextContent.Should().Be("debajo");
    }
}
