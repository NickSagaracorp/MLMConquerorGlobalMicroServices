using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CryptoSignups;

/// <summary>
/// AdminWeb no referencia AdminAPI: la pantalla de Entrax duplica el contrato por la forma del JSON. Aquí
/// se lee su marcado para que no se separen sin que nadie lo vea: mismas rutas y vistas que el
/// controlador, la fila con los mismos campos, los mismos cuatro roles en la página y en el menú, y nada
/// de pintar el enlace del QR.
/// </summary>
public class PantallaAdminEntraxSignupsTests
{
    private const string Ruta = "/admin/billing/entrax-signups";

    private static string Fichero(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        return File.ReadAllText(Path.Combine([dir!.FullName, .. partes]));
    }

    private static string Pagina() =>
        Fichero("MLMConquerorGlobalEdition.AdminWeb", "Components", "Pages", "AdminEntraxSignups.razor");

    private static string Menu() =>
        Fichero("MLMConquerorGlobalEdition.SharedComponents", "Components", "Layout", "AdminSidebarMenu.razor");

    [Fact]
    public void La_pagina_exige_los_cuatro_roles_de_cripto()
    {
        var pagina = Pagina();

        pagina.Should().Contain($"@page \"{Ruta}\"");
        pagina.Should().Contain("[Microsoft.AspNetCore.Authorization.Authorize(Roles = AppRoles.CryptoPaymentApprovers)]");
    }

    [Fact]
    public void Llama_a_las_rutas_y_vistas_del_controlador_y_pagina()
    {
        var pagina = Pagina();

        pagina.Should().Contain("api/v1/admin/entrax-signups?view=");
        pagina.Should().Contain("&page=").And.Contain("&pageSize=");
        pagina.Should().Contain("api/v1/admin/entrax-signups/{Uri.EscapeDataString(row.Id)}/recheck");
        foreach (var vista in new[] { AdminEntraxSignupsController.ViewOpen, AdminEntraxSignupsController.ViewReview,
                                      AdminEntraxSignupsController.ViewRecent })
            pagina.Should().Contain($"\"{vista}\"", $"la vista {vista} existe en el controlador");
    }

    [Fact]
    public void La_fila_de_la_pagina_tiene_los_campos_del_controlador_en_el_mismo_orden()
    {
        var m = Regex.Match(Pagina(), @"private record EntraxSignupRowDto\((?<campos>[^)]*)\)", RegexOptions.Singleline);
        m.Success.Should().BeTrue();

        var nombres = m.Groups["campos"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last());

        nombres.Should().Equal(typeof(AdminEntraxSignupsController.EntraxSignupRowDto)
            .GetConstructors().Single().GetParameters().Select(p => p.Name!));
    }

    [Fact]
    public void Usa_el_grid_de_Syncfusion_y_no_pinta_el_enlace_del_QR()
    {
        var pagina = Pagina();

        pagina.Should().Contain("<SfGrid TValue=\"EntraxSignupRowDto\"");
        pagina.Should().NotContain("QrLink");
    }

    [Fact]
    public void El_menu_la_enseña_solo_a_los_cuatro_roles_de_cripto()
    {
        var menu = Menu();
        var bloque = Regex.Match(menu,
            @"<AuthorizeView Roles=""@AppRoles\.CryptoPaymentApprovers"">(?<dentro>.*?)</AuthorizeView>",
            RegexOptions.Singleline);

        bloque.Success.Should().BeTrue();
        bloque.Groups["dentro"].Value.Should().Contain($"href=\"{Ruta}\"");
        Regex.Matches(menu, Regex.Escape($"href=\"{Ruta}\"")).Should().HaveCount(1, "una sola entrada, la de dentro");
    }
}
