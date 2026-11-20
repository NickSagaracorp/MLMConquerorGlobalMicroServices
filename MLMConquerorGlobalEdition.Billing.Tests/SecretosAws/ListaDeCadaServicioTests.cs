using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

/// <summary>
/// La lista SecretosAws:Secretos del appsettings.json de cada servicio y su fila en
/// deploy/aws/secretos-por-servicio.json tienen que ser iguales. Si se agrega un secreto en un lado
/// y no en el otro, o el servicio no arranca (le falta permiso) o tiene un permiso que no usa.
/// </summary>
public class ListaDeCadaServicioTests
{
    private static readonly string Raiz = BuscarRaiz();

    private static string BuscarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static JsonElement Tabla() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Raiz, "deploy", "aws", "secretos-por-servicio.json"))).RootElement;

    /// <summary>appsettings.json admite comentarios // y comas finales.</summary>
    private static JsonElement Appsettings(string servicio)
    {
        var ruta = Path.Combine(Raiz, $"MLMConquerorGlobalEdition.{servicio}", "appsettings.json");
        return JsonDocument.Parse(File.ReadAllText(ruta), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
        }).RootElement;
    }

    public static TheoryData<string> Servicios()
    {
        var datos = new TheoryData<string>();
        foreach (var s in Tabla().GetProperty("servicios").EnumerateObject()) datos.Add(s.Name);
        return datos;
    }

    [Fact]
    public void EstanLosOnceServicios()
    {
        Tabla().GetProperty("servicios").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "AdminAPI", "AdminWeb", "Billing", "BizCenter", "BizCenterWeb", "CommissionEngine",
            "RankEngine", "SharedAPICenter", "SignupAPI", "Signups", "TicketManagementSystem");
    }

    [Theory]
    [MemberData(nameof(Servicios))]
    public void LaListaDelAppsettingsEsSuFilaDeLaTabla(string servicio)
    {
        var fila = Tabla().GetProperty("servicios").GetProperty(servicio).GetProperty("secretos")
            .EnumerateArray().Select(e => e.GetString()).ToArray();

        var seccion = Appsettings(servicio).GetProperty("SecretosAws");
        var lista = seccion.GetProperty("Secretos").EnumerateArray().Select(e => e.GetString()).ToArray();

        lista.Should().Equal(fila);
        seccion.GetProperty("Habilitado").GetBoolean().Should().BeTrue();
        seccion.GetProperty("Entorno").GetString().Should().Be("local",
            "el repositorio apunta a local; staging y prod se eligen con SecretosAws__Entorno");
    }

    [Fact]
    public void CadaSecretoDeUnaFilaEstaDeclarado()
    {
        var declarados = Tabla().GetProperty("secretos").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        foreach (var s in Tabla().GetProperty("servicios").EnumerateObject())
            s.Value.GetProperty("secretos").EnumerateArray().Select(e => e.GetString())
                .Where(n => !declarados.Contains(n))
                .Should().BeEmpty($"{s.Name} usa un secreto no declarado");
    }

    [Theory]
    [MemberData(nameof(Servicios))]
    public void ElProgramLlamaAddSecretosDeAwsJustoDespuesDeCrearElBuilder(string servicio)
    {
        var program = File.ReadAllText(Path.Combine(Raiz, $"MLMConquerorGlobalEdition.{servicio}", "Program.cs"));
        Regex.IsMatch(program,
            @"var builder = WebApplication\.CreateBuilder\(args\);\s*(//[^\n]*\n\s*)*builder\.Configuration\.AddSecretosDeAws\(\);")
            .Should().BeTrue($"{servicio} tiene que leer sus secretos antes de que nada lea la configuración");
    }
}
