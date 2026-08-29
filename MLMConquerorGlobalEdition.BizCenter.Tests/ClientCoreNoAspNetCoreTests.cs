using System.Reflection;
using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El guardián de ClientCore: ahí dentro no puede entrar ASP.NET Core.
///
/// ClientCore existe por una sola razón — que la lógica de cliente (hablar con SignupAPI, poner el
/// Bearer, desenvolver el sobre, traducir el fallo a un código) se escriba una vez y sirva también
/// a las aplicaciones MAUI que vienen después. Una MAUI no puede referenciar el framework
/// compartido de ASP.NET Core, así que la primera dependencia de alojamiento web que se cuele aquí
/// deja el proyecto inservible para lo único que justifica su existencia.
///
/// Y se cuela sola. No hace falta una decisión de arquitectura: basta un <c>using
/// Microsoft.AspNetCore.Http;</c> puesto sin pensar para leer un HttpContext "solo un momento", y
/// a partir de ahí compila, pasa las pruebas, y nadie se entera hasta que alguien intenta montar
/// el proyecto móvil meses después y se encuentra el trabajo deshecho.
///
/// Por eso esto se comprueba sobre el ensamblado ya compilado y no leyendo el .csproj: lo que
/// importa no es lo que el fichero de proyecto declare, sino de qué depende de verdad el código
/// que salió del compilador.
///
/// Y se comprueba sobre el CIERRE TRANSITIVO, no sobre las referencias directas. Una referencia
/// de ensamblado es transitiva y un <c>FrameworkReference</c> también: a ClientCore no le hace
/// falta escribir un <c>using Microsoft.AspNetCore.Http;</c> para quedar inservible en móvil,
/// basta con que lo escriba algo de lo que él depende. Fue exactamente lo que pasó: ClientCore
/// nació limpio, pero SharedKernel —su única dependencia de proyecto— declaraba el framework
/// compartido de ASP.NET Core, así que una MAUI que referenciara ClientCore lo arrastraba igual.
/// Mirar solo un nivel dejaba esa puerta abierta y la prueba en verde.
///
/// SI ESTA PRUEBA SE PONE EN ROJO: lo que hay que quitar es la dependencia, no la prueba. El
/// mensaje de fallo trae el camino completo (quién referencia a quién) para saber en qué
/// proyecto hay que cortar. Si algo de ClientCore necesita de verdad el HttpContext, es que ese
/// algo no era compartible y su sitio es SharedComponents — igual que
/// <c>HttpContextAccessTokenProvider</c>, que se quedó allí precisamente por esto. Y si lo que
/// lo necesita está en SharedKernel, su sitio es SharedKernel.Server.
/// </summary>
public class ClientCoreNoAspNetCoreTests
{
    /// <summary>El ensamblado de ClientCore, alcanzado por un tipo suyo.</summary>
    private static Assembly ClientCoreAssembly => typeof(AuthApiGateway).Assembly;

    /// <summary>
    /// El invariante de verdad: en todo el árbol de dependencias de ClientCore no aparece ni un
    /// ensamblado de ASP.NET Core. Esto es lo que decide si el proyecto móvil va a poder
    /// referenciarlo o no.
    /// </summary>
    [Fact]
    public void ClientCore_NoReferenciaAspNetCoreEnTodoSuCierreTransitivo()
    {
        var (offenders, unresolved) = RecorrerCierreTransitivo(ClientCoreAssembly);

        offenders.Should().BeEmpty(
            "ClientCore tiene que poder compilarse dentro de una aplicación MAUI, y una MAUI no " +
            "puede referenciar ASP.NET Core. Cada línea de arriba es el camino desde ClientCore " +
            "hasta el ensamblado de ASP.NET Core que se coló; hay que cortar en el último " +
            "proyecto propio del camino. Lo que necesite HttpContext va en SharedComponents " +
            "(detrás de una abstracción como IAccessTokenProvider) o en SharedKernel.Server.");

        // Un ensamblado que no se pudo cargar es una rama del árbol que quedó sin explorar, y
        // una rama sin explorar es un agujero en el invariante. Se exige que no haya ninguna
        // para que un verde signifique de verdad "se miró todo".
        unresolved.Should().BeEmpty(
            "no se pudo cargar alguna dependencia, así que su subárbol quedó sin comprobar y " +
            "el verde de esta prueba no probaría nada sobre esa rama");
    }

    /// <summary>
    /// Recorre en anchura todo el árbol de referencias a partir de <paramref name="raiz"/>.
    ///
    /// Devuelve dos listas: los caminos que terminan en un ensamblado de ASP.NET Core, y los
    /// caminos que no se pudieron seguir porque el ensamblado no cargó. El nombre se comprueba
    /// ANTES de intentar cargarlo, a propósito: en un anfitrión sin el framework compartido de
    /// ASP.NET Core la carga fallaría y el infractor se colaría disfrazado de rama no resuelta.
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

                if (nombre.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase))
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
    public void LaPruebaMiraElEnsambladoDeClientCore()
    {
        ClientCoreAssembly.GetName().Name
            .Should().Be("MLMConquerorGlobalEdition.ClientCore");

        ClientCoreAssembly.GetReferencedAssemblies()
            .Should().NotBeEmpty("un ensamblado sin referencias significa que no se leyó bien");
    }

    /// <summary>
    /// El gateway no puede pedir nada de ASP.NET Core por su constructor. Es la puerta por la que
    /// entraría el acoplamiento —un <c>IHttpContextAccessor</c> "temporal"— y la que hay que
    /// mirar primero cuando alguien toque esta clase.
    /// </summary>
    [Fact]
    public void AuthApiGateway_NoPideNadaDeAspNetCoreEnSuConstructor()
    {
        var parameterAssemblies = typeof(AuthApiGateway)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Assembly.GetName().Name ?? string.Empty)
            .Distinct()
            .ToArray();

        parameterAssemblies.Should().NotContain(
            name => name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase),
            "el token de acceso entra por IAccessTokenProvider, que cada anfitrión implementa " +
            "como pueda: web desde el claim de la cookie, móvil desde el almacenamiento seguro.");
    }
}
