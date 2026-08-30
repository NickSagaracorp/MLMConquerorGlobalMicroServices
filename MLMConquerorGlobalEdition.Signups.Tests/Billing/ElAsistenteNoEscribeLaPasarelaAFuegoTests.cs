using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.Signups.Tests.Billing;

/// <summary>
/// El guardián del asistente: en las pantallas de alta no se escribe a mano ni el nombre de la
/// pasarela ni ningún token.
/// </summary>
/// <remarks>
/// POR QUÉ ESTA PRUEBA LEE CÓDIGO FUENTE, QUE NO ES LO NORMAL. El fallo original no fue una lógica
/// equivocada: fue un objeto anónimo con dos literales dentro —<c>GatewayToken = "manual"</c> y
/// <c>Gateway = "Stripe"</c>— escrito directamente en el marcado. Un objeto anónimo dentro de un
/// método de un componente Razor no se puede instanciar desde una prueba, no tiene tipo al que
/// agarrarse y no aparece en ninguna interfaz: no hay forma de comprobarlo por reflexión. Y era
/// exactamente el sitio donde el error podía volver, porque es el sitio donde es más cómodo
/// escribirlo.
///
/// Las pruebas de <c>TokenizacionDeTarjetaTests</c> cubren que <c>CardPayment</c> hace lo correcto.
/// Esta cubre lo otro: que el asistente sigue pasando por ahí y no se ha vuelto a montar el bloque
/// a mano al lado. Las dos hacen falta; ninguna sustituye a la otra.
///
/// SI SE PONE EN ROJO: el arreglo no es relajar el patrón, es sacar el literal de la pantalla y
/// hacer que salga de la tokenización, como en <see cref="MLMConquerorGlobalEdition.SharedKernel.Billing.CardPayment"/>.
/// </remarks>
public class ElAsistenteNoEscribeLaPasarelaAFuegoTests
{
    /// <summary>Las pantallas de alta que cobran con tarjeta. Las dos tenían el mismo fallo copiado.</summary>
    public static TheoryData<string> Asistentes() => new()
    {
        "Pages/Signup.razor",
        "Pages/MemberJoin.razor"
    };

    /// <summary>
    /// Ningún literal donde tiene que ir lo que devuelve la pasarela.
    /// </summary>
    [Theory]
    [MemberData(nameof(Asistentes))]
    public void ElAsistente_NoAsignaLaPasarelaNiLosTokensConUnLiteral(string ruta)
    {
        var codigo = SinComentarios(LeerAsistente(ruta));

        var literales = new[]
        {
            (Campo: "Gateway",      Patron: @"\bGateway\s*=\s*""" ),
            (Campo: "GatewayToken", Patron: @"\bGatewayToken\s*=\s*"""),
            (Campo: "CardToken",    Patron: @"\bCardToken\s*=\s*""" )
        };

        foreach (var (campo, patron) in literales)
        {
            Regex.IsMatch(codigo, patron).Should().BeFalse(
                $"'{campo}' en {ruta} tiene que salir del resultado de la tokenización, no de una " +
                "cadena escrita en la pantalla. Fue así como el alta con tarjeta se rompió: " +
                @"Gateway = ""Stripe"" (con mayúscula) y GatewayToken = ""manual"" los rechaza " +
                "CreditCardInfoDtoValidator, y el 400 salía como un mensaje genérico de error.");
        }
    }

    /// <summary>
    /// Y la otra mitad: que el bloque de tarjeta se sigue construyendo donde se decidió. Sin esto,
    /// borrar la llamada y volver a un objeto anónimo sin literales —tomando los campos de
    /// variables— dejaría la prueba de arriba en verde.
    /// </summary>
    [Theory]
    [MemberData(nameof(Asistentes))]
    public void ElAsistente_ConstruyeElPagoConTarjetaDondeSeDecidio(string ruta)
    {
        var codigo = SinComentarios(LeerAsistente(ruta));

        codigo.Should().Contain("CardPayment.PrepareAsync",
            $"{ruta} tiene que tokenizar por el único camino que garantiza de dónde sale cada campo");
        codigo.Should().Contain("CardPayment.Validate",
            $"{ruta} comprueba la tarjeta antes de molestar a la pasarela");
    }

    /// <summary>
    /// El modo de render de las pantallas de alta es parte de la decisión de PCI DSS y por eso se
    /// vigila igual que los literales.
    /// </summary>
    /// <remarks>
    /// Con <c>InteractiveAuto</c> la PRIMERA visita corre en el servidor sobre un circuito, y sobre
    /// un circuito cada pulsación de un <c>@oninput</c> viaja hasta nosotros — incluidas las del
    /// campo del número de tarjeta. Es decir: el PAN llegaba a nuestros servidores tecla a tecla
    /// aunque el cuerpo de la petición a la API no lo llevara nunca. Con
    /// <c>InteractiveWebAssembly</c> la página se prepinta como marcado estático y solo se vuelve
    /// interactiva ya dentro del navegador.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Asistentes))]
    public void ElAsistente_SeVuelveInteractivoSoloEnElNavegador(string ruta)
    {
        var codigo = LeerAsistente(ruta);

        Regex.IsMatch(codigo, @"@rendermode\s+InteractiveWebAssembly").Should().BeTrue(
            $"{ruta} tiene que volverse interactivo en el navegador y no sobre un circuito de " +
            "servidor: es lo que impide que el número de tarjeta viaje hasta nosotros tecla a tecla");

        Regex.IsMatch(codigo, @"@rendermode\s+InteractiveAuto").Should().BeFalse(
            "InteractiveAuto ejecuta la primera visita en el servidor, con circuito");
        Regex.IsMatch(codigo, @"@rendermode\s+InteractiveServer").Should().BeFalse(
            "InteractiveServer ejecuta TODAS las visitas en el servidor, con circuito");
    }

    // ===============================================================================================

    /// <summary>
    /// Quita los comentarios antes de buscar. Los comentarios de estas pantallas explican
    /// literalmente cuáles eran los valores a fuego —hace falta que lo expliquen— y sin esto la
    /// prueba se dispararía con su propia documentación.
    /// </summary>
    private static string SinComentarios(string codigo)
    {
        var sinBloquesRazor = Regex.Replace(codigo, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        var sinBloquesC     = Regex.Replace(sinBloquesRazor, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var lineas = sinBloquesC
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join('\n', lineas);
    }

    private static string LeerAsistente(string rutaRelativa)
    {
        var ruta = Path.Combine(RaizDelRepositorio(), "MLMConquerorGlobalEdition.Signups.Client", rutaRelativa);

        File.Exists(ruta).Should().BeTrue(
            $"la pantalla de alta tiene que estar en {ruta}; si se movió, hay que actualizar esta " +
            "prueba, no borrarla");

        return File.ReadAllText(ruta);
    }

    /// <summary>
    /// Sube desde el binario de pruebas hasta encontrar la solución. Si no la encuentra FALLA a
    /// propósito: una prueba que se salta a sí misma cuando no encuentra el fichero es una prueba
    /// que un día deja de comprobar nada sin que nadie se entere.
    /// </summary>
    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "MLMConquerorGlobalEdition.slnx")))
                return directorio.FullName;

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró MLMConquerorGlobalEdition.slnx subiendo desde " + AppContext.BaseDirectory +
            ". Esta prueba lee el código de las pantallas de alta y necesita la raíz del repositorio.");
    }
}
