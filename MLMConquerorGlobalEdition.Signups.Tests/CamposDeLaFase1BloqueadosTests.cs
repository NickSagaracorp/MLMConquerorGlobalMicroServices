namespace MLMConquerorGlobalEdition.Signups.Tests;

/// <summary>
/// Regla del alta: ningún campo editable cuyo cambio se pierda en silencio. Una vez creada el alta (fase 1)
/// los datos que viajaron en esa petición no se reenvían, así que sus controles se deshabilitan con
/// <c>Locked</c>. Los de pago (tarjeta, token, método, facturación) son de la fase 3 y siguen editables.
/// Se lee el marcado de las dos páginas porque un campo nuevo sin bloquear es justo lo que hay que vigilar.
/// </summary>
public class CamposDeLaFase1BloqueadosTests
{
    private static readonly string[] ComunesALasDos =
    [
        "_firstName", "_lastName", "_ssn", "_whatsApp", "_cellPhone", "_address", "_city", "_postalCode",
        "_province", "_email", "_email2", "_password", "_password2", "_showCountryModal = true"
    ];

    private static readonly string[] SoloAmbassador =
    [
        "_dateOfBirth", "_showBusiness", "_businessName", "_ein", "OnHandleInput", "_showWaPicker = !_showWaPicker",
        "_showCellPicker = !_showCellPicker"
    ];

    private static readonly string[] SoloMember = ["_waCountryCode\"", "_cellCountryCode\"", "@onchange=\"OnCountryChange\""];

    private static string Pagina(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        var texto = File.ReadAllText(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.Signups.Client", "Pages", nombre));
        return texto[..texto.IndexOf("@code", StringComparison.Ordinal)];   // solo el marcado
    }

    /// <summary>Etiquetas (de '&lt;' al '&gt;' que cierra, sin contar '=&gt;') que contienen el trozo.</summary>
    private static List<string> Etiquetas(string marcado, string trozo)
    {
        var etiquetas = new List<string>();
        for (var i = marcado.IndexOf(trozo, StringComparison.Ordinal); i >= 0;
             i = marcado.IndexOf(trozo, i + 1, StringComparison.Ordinal))
        {
            var ini = marcado.LastIndexOf('<', i);
            var fin = i;
            while (fin < marcado.Length && !(marcado[fin] == '>' && marcado[fin - 1] != '=')) fin++;
            var etiqueta = marcado[ini..Math.Min(fin + 1, marcado.Length)];
            if (etiqueta.StartsWith("<input") || etiqueta.StartsWith("<select") || etiqueta.StartsWith("<button"))
                etiquetas.Add(etiqueta);
        }
        return etiquetas;
    }

    private static void ExigeBloqueo(string marcado, string clave)
    {
        // "a|b": el control que contiene a y b a la vez (p. ej. el botón del país).
        var partes = clave.Split('|');
        var candidatas = Etiquetas(marcado, partes[0]).Where(t => partes.All(t.Contains)).ToList();
        candidatas.Should().NotBeEmpty($"el campo {clave} debe seguir existiendo en la página");
        foreach (var etiqueta in candidatas)
            etiqueta.Should().Contain("disabled=\"@Locked\"", $"{clave} viaja en la fase 1 y no se reenvía");
    }

    [Theory]
    [InlineData("Signup.razor")]
    [InlineData("MemberJoin.razor")]
    public void Los_datos_de_la_fase_1_se_bloquean_con_el_alta_creada(string pagina)
    {
        var marcado = Pagina(pagina);
        var claves = ComunesALasDos.Concat(pagina == "Signup.razor" ? SoloAmbassador : SoloMember);

        foreach (var clave in claves)
        {
            // Los campos de entrada llevan su binding; el correo y la contraseña, su manejador.
            var buscado = clave switch
            {
                "_email"     => "@oninput=\"OnEmailInput\"",
                "_email2"    => "@oninput=\"OnEmail2Input\"",
                "_password"  => "@oninput=\"OnPasswordInput\"",
                "_password2" => "@oninput=\"OnPassword2Input\"",
                _            => clave
            };
            ExigeBloqueo(marcado, buscado);
        }
    }

    [Theory]
    [InlineData("Signup.razor")]
    [InlineData("MemberJoin.razor")]
    public void El_nivel_y_la_opcion_sin_membresia_tambien_se_bloquean(string pagina)
    {
        var marcado = Pagina(pagina);

        ExigeBloqueo(marcado, "name=\"membership\"");
        ExigeBloqueo(marcado, "@onclick=\"() => _showNoMemberModal = true\"");
    }

    [Theory]
    [InlineData("Signup.razor")]
    [InlineData("MemberJoin.razor")]
    public void Los_campos_de_pago_siguen_editables(string pagina)
    {
        var marcado = Pagina(pagina);

        foreach (var pago in new[] { "@bind=\"_ccFirst\"", "@bind=\"_ccLast\"", "@bind=\"_ccCvv\"", "@bind=\"_couponCode\"", "@bind=\"_billAddress\"" })
            Etiquetas(marcado, pago).Should().OnlyContain(t => !t.Contains("@Locked"), $"{pago} es de la fase 3");
    }
}
