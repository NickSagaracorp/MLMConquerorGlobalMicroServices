using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Grid;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Revision del bloque 5 (I4): el aviso de equipo grande es un componente comun (LargeTeamNotice) y las tres rejillas de
/// equipo que el servidor puede limitar (Mi equipo de los dos arboles y los eventos del equipo) le pasan OnLimited.
/// </summary>
public class AvisoDeEquipoGrandeTests : BunitContext
{
    [Fact]
    public async Task Se_muestra_solo_cuando_el_servidor_limita()
    {
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        var aviso = Render<LargeTeamNotice>();
        aviso.FindAll(".large-team-notice").Should().BeEmpty();

        await aviso.InvokeAsync(() => aviso.Instance.SetAsync(true));
        aviso.Find(".large-team-notice").TextContent.Should().Be("Team.LargeTeamLimited");

        await aviso.InvokeAsync(() => aviso.Instance.SetAsync(false));
        aviso.FindAll(".large-team-notice").Should().BeEmpty();
    }

    [Theory]
    [InlineData("DualTeamMyTeam.razor")]
    [InlineData("EnrollmentTeamMyTeam.razor")]
    [InlineData("TeamEventRegistrations.razor")]
    public void La_rejilla_pasa_OnLimited_al_aviso_comun(string componente)
    {
        var fuente = File.ReadAllText(Path.Combine(ReportesDb.RaizDelRepositorio(),
            "MLMConquerorGlobalEdition.SharedComponents", "Components", "Team", componente));

        fuente.Should().Contain("<LargeTeamNotice @ref=\"_aviso\" />");
        fuente.Should().Contain("OnLimited=\"@(l => _aviso?.SetAsync(l) ?? Task.CompletedTask)\"");
        fuente.Should().NotContain("Team.LargeTeamLimited", "el texto lo pinta el aviso comun, no cada rejilla");
    }
}
