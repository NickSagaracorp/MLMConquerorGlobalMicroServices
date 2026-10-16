using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class EntraxRegistroTests
{
    private static ServiceProvider Contenedor(EntraxOptions o)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton(Mock.Of<IDateTimeProvider>());
        s.AddEntrax(o);
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
}
