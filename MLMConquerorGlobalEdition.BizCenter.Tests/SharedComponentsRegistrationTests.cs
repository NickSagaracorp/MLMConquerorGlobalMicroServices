using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.SharedComponents.Constants;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El registro del contexto de vista.
/// </summary>
/// <remarks>
/// Existe por un fallo concreto: <c>AddSharedComponents</c> registraba la interfaz y el tipo
/// concreto con dos <c>AddScoped</c> independientes, lo que fabrica dos instancias distintas por
/// ámbito. Los inicializadores inyectan el tipo concreto y llaman a <c>SetContext</c>; los
/// componentes inyectan la interfaz. Cada uno hablaba con un objeto diferente y el contexto no
/// llegaba a la pantalla — en las MAUI, donde la semilla es la vacía, la impersonación de
/// AdminApp no surtía ningún efecto.
///
/// El compilador no ve esto y ninguna prueba de <c>ViewContextService</c> lo veía tampoco, porque
/// todas construyen el servicio a mano. Solo se cae si se resuelve por el contenedor, que es lo
/// que hace la aplicación de verdad.
/// </remarks>
public class SharedComponentsRegistrationTests
{
    private static ServiceProvider Build() =>
        new ServiceCollection().AddSharedComponents().BuildServiceProvider();

    [Fact]
    public void AddSharedComponents_LaInterfazYElTipoConcreto_SonLaMismaInstancia()
    {
        using var provider = Build();
        using var scope    = provider.CreateScope();

        var porInterfaz = scope.ServiceProvider.GetRequiredService<IViewContextService>();
        var porConcreto = scope.ServiceProvider.GetRequiredService<ViewContextService>();

        porInterfaz.Should().BeSameAs(porConcreto);
    }

    /// <summary>
    /// La consecuencia observable, que es lo que de verdad importa: lo que escribe quien inicializa
    /// tiene que poder leerlo quien pinta.
    /// </summary>
    [Fact]
    public void SetContext_SobreElTipoConcreto_LoVeQuienInyectaLaInterfaz()
    {
        using var provider = Build();
        using var scope    = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ViewContextService>()
             .SetContext("mem-9", "usr-9", isImpersonating: true, isAdminContext: true,
                         [AppRoles.Ambassador]);

        var visto = scope.ServiceProvider.GetRequiredService<IViewContextService>();

        visto.ViewingMemberId.Should().Be("mem-9");
        visto.IsImpersonating.Should().BeTrue();
    }

    /// <summary>Ámbitos distintos siguen siendo contextos distintos: no se filtra entre peticiones.</summary>
    [Fact]
    public void AmbitosDistintos_NoCompartenContexto()
    {
        using var provider = Build();
        using var uno = provider.CreateScope();
        using var dos = provider.CreateScope();

        uno.ServiceProvider.GetRequiredService<ViewContextService>()
           .SetContext("mem-uno", "usr-uno", false, false, [AppRoles.Ambassador]);

        dos.ServiceProvider.GetRequiredService<IViewContextService>()
           .ViewingMemberId.Should().NotBe("mem-uno");
    }
}
