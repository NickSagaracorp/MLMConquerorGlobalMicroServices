using System.Text.Json;
using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Seguridad;

/// <summary>
/// El segundo factor es obligatorio para todo el que entra al portal de administración. Hay tres
/// listas que tienen que coincidir y viven en tres archivos:
///
/// - <c>AllowedRoles</c> en AdminWeb/Program.cs: quién puede entrar al portal.
/// - <c>Auth:TwoFactor:MandatoryRoles</c> en SignupAPI: el login obliga a enrolarse y la API no deja
///   apagarlo.
/// - La misma clave en AdminWeb: el panel de cuenta no ofrece apagarlo.
///
/// Un rol que entre al portal sin estar en la de SignupAPI entra sin segundo factor; uno que esté en
/// la de SignupAPI y no en la de AdminWeb ve el botón de apagarlo y se lleva un error al pulsarlo.
/// </summary>
public class SegundoFactorObligatorioEnAdminTests
{
    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private static string[] RolesDelPortal()
    {
        var program = File.ReadAllText(Path.Combine(Raiz(), "MLMConquerorGlobalEdition.AdminWeb", "Program.cs"));
        var bloque  = Regex.Match(program, @"AllowedRoles\s*=\s*\[(?<roles>[^\]]*)\]");
        bloque.Success.Should().BeTrue("AdminWeb declara los roles que pueden entrar al portal");
        return Regex.Matches(bloque.Groups["roles"].Value, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
    }

    private static string[] Obligatorios(string proyecto)
    {
        using var json = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Raiz(), proyecto, "appsettings.json")),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return json.RootElement.GetProperty("Auth").GetProperty("TwoFactor").GetProperty("MandatoryRoles")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    [Fact]
    public void Todo_rol_que_entra_al_admin_tiene_el_segundo_factor_obligatorio_en_el_login()
    {
        var portal = RolesDelPortal();
        portal.Should().Contain(["SuperAdmin", "SupportLevel1", "SupportLevel2", "SupportLevel3", "SupportManager"]);
        Obligatorios("MLMConquerorGlobalEdition.SignupAPI").Should().Contain(portal);
    }

    [Fact]
    public void El_panel_del_admin_usa_la_misma_lista_que_el_login()
    {
        Obligatorios("MLMConquerorGlobalEdition.AdminWeb")
            .Should().BeEquivalentTo(Obligatorios("MLMConquerorGlobalEdition.SignupAPI"));
    }
}
