using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class EntraxRegistroTests
{
    private static ServiceProvider Contenedor(EntraxOptions o, bool isDevelopment = true)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton(Mock.Of<IDateTimeProvider>());
        s.AddEntrax(o, isDevelopment);
        return s.BuildServiceProvider();
    }

    [Fact]
    public void Con_el_fake_el_cliente_es_el_mismo_singleton_que_marca_los_pagos()
    {
        using var sp = Contenedor(new EntraxOptions { UseFake = true });

        var cliente = sp.GetRequiredService<IEntraxClient>();
        cliente.Should().BeOfType<FakeEntraxClient>();
        cliente.Should().BeSameAs(sp.GetRequiredService<FakeEntraxClient>());
        sp.GetRequiredService<IEntraxClient>().Should().BeSameAs(cliente);
    }

    [Fact]
    public void Sin_el_fake_el_cliente_es_el_real_y_el_fake_no_existe()
    {
        using var sp = Contenedor(new EntraxOptions { Enabled = true, KeyId = "k", Secret = "s" });

        sp.GetRequiredService<IEntraxClient>().Should().BeOfType<EntraxClient>();
        sp.GetService<FakeEntraxClient>().Should().BeNull();
    }

    [Fact]
    public void Las_opciones_se_registran_tal_cual()
    {
        var o = new EntraxOptions { Enabled = true };
        using var sp = Contenedor(o);

        sp.GetRequiredService<EntraxOptions>().Should().BeSameAs(o);
    }

    [Fact]
    public void El_fake_fuera_de_Development_no_se_registra_aunque_nadie_llame_a_la_guarda()
    {
        var s = new ServiceCollection();

        var act = () => s.AddEntrax(new EntraxOptions { UseFake = true }, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*UseFake*");
        s.Should().NotContain(d => d.ServiceType == typeof(FakeEntraxClient));
    }

    [Fact]
    public void Encendido_sin_credenciales_fuera_de_Development_tampoco_se_registra()
    {
        var act = () => new ServiceCollection().AddEntrax(new EntraxOptions { Enabled = true }, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>();
    }
}
