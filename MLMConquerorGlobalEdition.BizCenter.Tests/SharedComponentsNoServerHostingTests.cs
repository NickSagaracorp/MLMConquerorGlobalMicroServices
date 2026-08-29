using System.Reflection;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El guardián de SharedComponents: ahí dentro no puede entrar el alojamiento web.
///
/// Hermana de <see cref="ClientCoreNoAspNetCoreTests"/>, pero con la línea en otro sitio, y esa
/// diferencia es lo importante de este archivo.
///
/// ClientCore no puede ver NI UN ensamblado de ASP.NET Core: es lógica de cliente y no pinta nada.
/// SharedComponents sí, porque es una biblioteca de componentes Razor y una MAUI Blazor Hybrid
/// ejecuta Blazor de verdad: <c>ComponentBase</c>, <c>EditForm</c>, <c>@onclick</c> y
/// <c>AuthorizeView</c> viven todos bajo <c>Microsoft.AspNetCore.*</c> y los carga
/// <c>Microsoft.AspNetCore.Components.WebView.Maui</c> dentro del teléfono. Prohibir el prefijo
/// entero aquí sería prohibir Blazor.
///
/// ASÍ QUE LA LÍNEA NO ESTÁ EN EL PREFIJO DEL NOMBRE, ESTÁ EN SI EL ENSAMBLADO NECESITA UN SERVIDOR
/// DEBAJO. Y por eso esto es una lista de permitidos y no un patrón: dentro de
/// <c>Microsoft.AspNetCore.Components.*</c> conviven las dos cosas. <c>Components.Web</c> y
/// <c>Components.Forms</c> son del motor de componentes y viajan en el APK;
/// <c>Components.Server</c> (el circuito de SignalR) y <c>Components.Endpoints</c> (el renderizado
/// desde el enrutador de ASP.NET Core) son de servidor y no existen en Android. Un patrón
/// <c>Components.*</c> los habría dejado pasar a los cuatro.
///
/// La lista de abajo es exactamente lo que una MAUI Blazor Hybrid carga por su cuenta. Todo lo
/// demás que empiece por <c>Microsoft.AspNetCore</c> —Http, Routing, Hosting, Mvc, Authentication,
/// DataProtection, Antiforgery…— solo existe en el framework compartido
/// <c>Microsoft.AspNetCore.App</c>, que no tiene paquete de tiempo de ejecución para Android ni
/// para iOS.
///
/// POR QUÉ HACE FALTA ESTA PRUEBA Y NO BASTA CON QUE LAS MAUI COMPILEN: hasta esta tarea compilaban,
/// y estaban rotas. SharedComponents declaraba <c>&lt;FrameworkReference Include="Microsoft.AspNetCore.App" /&gt;</c>
/// para el cableado del área de cuenta, un FrameworkReference es transitivo, y cada MAUI lo borraba
/// a mano con un <c>&lt;FrameworkReference Remove="…" /&gt;</c> colocado DESPUÉS de la recolección.
/// Eso dejaba puestas las referencias de COMPILACIÓN y quitaba solo el paquete de EJECUCIÓN: el
/// código se empaquetaba en el APK compilado contra ensamblados que el dispositivo no iba a tener.
/// Verde al construir, excepción al arrancar. Una prueba sobre el cierre transitivo del ensamblado
/// ya compilado ve lo que el compilador vio, que es justo lo que aquel apaño escondía.
///
/// SI ESTA PRUEBA SE PONE EN ROJO: lo que hay que mover es el código, no ampliar la lista. El
/// mensaje de fallo trae el camino completo (quién referencia a quién). Lo que necesite HttpContext,
/// cookies, endpoints o autenticación de servidor va en
/// MLMConquerorGlobalEdition.SharedComponents.Server, que es donde están AccountEndpoints,
/// ChallengeCookies y las dos clases de datos de página. Ampliar la lista solo es correcto si de
/// verdad aparece un ensamblado nuevo de Blazor que MAUI cargue, y eso se comprueba compilando
/// las dos MAUI para android, no razonando sobre el nombre.
/// </summary>
public class SharedComponentsNoServerHostingTests
{
    /// <summary>El ensamblado de SharedComponents, alcanzado por un tipo suyo.</summary>
    private static Assembly SharedComponentsAssembly => typeof(ViewContextService).Assembly;

    /// <summary>
    /// Los ensamblados de ASP.NET Core que una MAUI Blazor Hybrid carga de verdad, y que por tanto
    /// pueden aparecer en el cierre de SharedComponents.
    ///
    /// Se comprueban por nombre EXACTO y no por prefijo: <c>Microsoft.AspNetCore.Components.Server</c>
    /// empieza igual que <c>Microsoft.AspNetCore.Components</c> y es precisamente de los que no
    /// pueden estar.
    /// </summary>
    private static readonly HashSet<string> BlazorPermitidosEnMovil = new(StringComparer.OrdinalIgnoreCase)
    {
        // El motor de componentes: ComponentBase, RenderTreeBuilder, EventCallback, ParameterView.
        "Microsoft.AspNetCore.Components",
        // El enlace con el DOM: @onclick, los eventos del navegador, InputText y compañía.
        "Microsoft.AspNetCore.Components.Web",
        // EditForm, DataAnnotationsValidator, ValidationMessage.
        "Microsoft.AspNetCore.Components.Forms",
        // AuthorizeView y CascadingAuthenticationState, que usan los layouts compartidos.
        "Microsoft.AspNetCore.Components.Authorization",
        // El anfitrión Blazor dentro del WebView de MAUI. No lo referencia SharedComponents, pero
        // si algún día lo hiciera seguiría siendo legítimo: es literalmente lo que ejecuta esto
        // en el teléfono.
        "Microsoft.AspNetCore.Components.WebView",
        "Microsoft.AspNetCore.Components.WebView.Maui",
        // [Authorize] y la evaluación de políticas que hay detrás de AuthorizeView. Es el
        // AddAuthorizationCore() que ya llaman los dos MauiProgram.
        "Microsoft.AspNetCore.Authorization",
        // Dependencia de Authorization; solo son los atributos de metadatos, sin nada de hosting.
        "Microsoft.AspNetCore.Metadata",
    };

    /// <summary>
    /// El invariante de verdad: en todo el árbol de dependencias de SharedComponents no aparece
    /// ningún ensamblado de ASP.NET Core que no sea de los que MAUI carga. Esto es lo que decide
    /// si las dos aplicaciones móviles van a poder referenciar esta biblioteca sin el apaño.
    /// </summary>
    [Fact]
    public void SharedComponents_NoReferenciaAlojamientoWebEnTodoSuCierreTransitivo()
    {
        var (offenders, unresolved) = RecorrerCierreTransitivo(SharedComponentsAssembly);

        offenders.Should().BeEmpty(
            "SharedComponents tiene que poder compilarse Y EJECUTARSE dentro de una aplicación " +
            "MAUI, y en Android no existe el framework compartido Microsoft.AspNetCore.App. Cada " +
            "línea de arriba es el camino desde SharedComponents hasta el ensamblado de " +
            "alojamiento web que se coló; hay que cortar en el último proyecto propio del camino. " +
            "Lo que necesite HttpContext, cookies o endpoints va en SharedComponents.Server.");

        // Un ensamblado que no se pudo cargar es una rama del árbol que quedó sin explorar, y una
        // rama sin explorar es un agujero en el invariante. Se exige que no haya ninguna para que
        // un verde signifique de verdad "se miró todo".
        unresolved.Should().BeEmpty(
            "no se pudo cargar alguna dependencia, así que su subárbol quedó sin comprobar y el " +
            "verde de esta prueba no probaría nada sobre esa rama");
    }

    /// <summary>
    /// Recorre en anchura todo el árbol de referencias a partir de <paramref name="raiz"/>.
    ///
    /// El nombre se comprueba ANTES de intentar cargarlo, a propósito: en un anfitrión sin el
    /// framework compartido de ASP.NET Core la carga fallaría y el infractor se colaría disfrazado
    /// de rama no resuelta.
    /// </summary>
    private static (IReadOnlyList<string> Offenders, IReadOnlyList<string> Unresolved)
        RecorrerCierreTransitivo(Assembly raiz)
    {
        var raizNombre = raiz.GetName().Name ?? string.Empty;
        var vistos     = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { raizNombre };
        var pendientes = new Queue<(Assembly Ensamblado, string Camino)>();
        var offenders  = new List<string>();
        var unresolved = new List<string>();

        pendientes.Enqueue((raiz, raizNombre));

        while (pendientes.Count > 0)
        {
            var (ensamblado, camino) = pendientes.Dequeue();

            foreach (var referencia in ensamblado.GetReferencedAssemblies())
            {
                var nombre        = referencia.Name ?? string.Empty;
                var caminoDestino = $"{camino} -> {nombre}";

                if (nombre.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)
                    && !BlazorPermitidosEnMovil.Contains(nombre))
                {
                    offenders.Add(caminoDestino);
                    continue;
                }

                if (!vistos.Add(nombre)) continue;

                try
                {
                    pendientes.Enqueue((Assembly.Load(referencia), caminoDestino));
                }
                catch (Exception ex)
                {
                    unresolved.Add($"{caminoDestino} ({ex.GetType().Name})");
                }
            }
        }

        return (offenders, unresolved);
    }

    /// <summary>
    /// Que la prueba de arriba esté mirando el ensamblado que cree. Sin esto, un renombrado o una
    /// fusión de proyectos podría dejarla apuntando a otro sitio y verde para siempre sin
    /// comprobar nada.
    /// </summary>
    [Fact]
    public void LaPruebaMiraElEnsambladoDeSharedComponents()
    {
        SharedComponentsAssembly.GetName().Name
            .Should().Be("MLMConquerorGlobalEdition.SharedComponents");

        SharedComponentsAssembly.GetReferencedAssemblies()
            .Should().NotBeEmpty("un ensamblado sin referencias significa que no se leyó bien");
    }

    /// <summary>
    /// Y que la lista de permitidos no se haya quedado vacía ni se haya convertido en un comodín.
    ///
    /// Sin esto, la forma más fácil de "arreglar" un fallo de esta prueba —meter en la lista todo
    /// lo que salga en el mensaje— la dejaría verde para siempre sin que nadie lo notara al leer
    /// el diff. Lo que se comprueba es que sigue habiendo una línea: que Blazor pasa y que el
    /// alojamiento de servidor no.
    /// </summary>
    [Fact]
    public void LaListaDePermitidosSigueSiendoUnaLineaYNoUnComodin()
    {
        BlazorPermitidosEnMovil.Should().Contain("Microsoft.AspNetCore.Components.Web",
            "sin Blazor esta biblioteca no existe");

        BlazorPermitidosEnMovil.Should().NotContain(
            [
                // El circuito de SignalR y el renderizado desde el enrutador de ASP.NET Core: son
                // Components.* y son de servidor. Son la razón de que esto sea una lista y no un
                // patrón.
                "Microsoft.AspNetCore.Components.Server",
                "Microsoft.AspNetCore.Components.Endpoints",
                // HttpContext y las cookies. Es por donde se coló todo lo que esta tarea sacó.
                "Microsoft.AspNetCore.Http",
                "Microsoft.AspNetCore.Http.Abstractions",
                // Los ClaimsPrincipal.FindFirstValue que había que reescribir a FindFirst()?.Value.
                "Microsoft.AspNetCore.Authentication.Abstractions",
                // El enrutamiento y los endpoints de las minimal API.
                "Microsoft.AspNetCore.Routing",
                "Microsoft.AspNetCore.Mvc.Abstractions",
            ],
            "estos son de servidor; si alguno hiciera falta es que el código que lo necesita está " +
            "en el proyecto equivocado");
    }
}
