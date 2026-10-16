using System.Reflection;
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
    public void La_fila_de_la_pagina_tiene_los_campos_del_controlador_con_su_tipo_y_en_el_mismo_orden()
    {
        var m = Regex.Match(Pagina(), @"private record EntraxSignupRowDto\((?<campos>[^)]*)\)", RegexOptions.Singleline);
        m.Success.Should().BeTrue();

        var deLaPagina = m.Groups["campos"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(p => $"{p[0]} {p[1]}");

        var nulabilidad = new NullabilityInfoContext();
        var delControlador = typeof(AdminEntraxSignupsController.EntraxSignupRowDto)
            .GetConstructors().Single().GetParameters()
            .Select(p => $"{Alias(p, nulabilidad)} {p.Name}");

        deLaPagina.Should().Equal(delControlador);
    }

    /// <summary>El tipo tal como se escribe en C#: <c>string?</c>, <c>decimal?</c>, <c>DateTime</c>…</summary>
    private static string Alias(ParameterInfo p, NullabilityInfoContext ctx)
    {
        var tipo     = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
        var anulable = Nullable.GetUnderlyingType(p.ParameterType) is not null
                       || ctx.Create(p).WriteState == NullabilityState.Nullable;
        var nombre = tipo == typeof(string)  ? "string"
                   : tipo == typeof(decimal) ? "decimal"
                   : tipo == typeof(int)     ? "int"
                   : tipo == typeof(bool)    ? "bool"
                   : tipo.Name;
        return anulable ? nombre + "?" : nombre;
    }

    [Fact]
    public void Usa_el_grid_de_Syncfusion_y_no_pinta_el_enlace_del_QR()
    {
        var pagina = Pagina();

        pagina.Should().Contain("<SfGrid TValue=\"EntraxSignupRowDto\"");
        pagina.Should().NotContain("QrLink");
    }

    [Fact]
    public void El_grid_no_filtra_ni_ordena_porque_solo_veria_la_pagina_cargada()
    {
        var grid = Regex.Match(Pagina(), @"<SfGrid TValue=""EntraxSignupRowDto""[^>]*>", RegexOptions.Singleline).Value;

        grid.Should().NotContain("AllowFiltering").And.NotContain("AllowSorting");
        Pagina().Should().NotContain("<GridFilterSettings");
    }

    [Fact]
    public void Re_check_ignora_el_doble_clic_y_explica_un_403()
    {
        var pagina = Pagina();
        var recheck = pagina[pagina.IndexOf("private async Task RecheckAsync", StringComparison.Ordinal)..];

        recheck.Should().Contain("if (_busyId is not null) return;");
        recheck.Should().Contain("HttpStatusCode.Forbidden").And.Contain("permission");
    }

    [Fact]
    public void Un_error_al_cargar_vacia_la_lista()
    {
        var pagina = Pagina();
        var carga = pagina[pagina.IndexOf("private async Task LoadAsync", StringComparison.Ordinal)..];
        var catchDeCarga = carga[carga.IndexOf("catch", StringComparison.Ordinal)..carga.IndexOf("finally", StringComparison.Ordinal)];

        catchDeCarga.Should().Contain("_rows").And.Contain("_totalCount = 0");
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
