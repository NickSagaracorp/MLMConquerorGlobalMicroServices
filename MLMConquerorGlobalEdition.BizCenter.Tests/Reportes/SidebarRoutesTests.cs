using MLMConquerorGlobalEdition.SharedComponents.Components.Layout;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class SidebarRoutesTests
{
    [Theory]
    [InlineData("https://bc.test/team/residuals")]
    [InlineData("https://bc.test/team/dual-myteam")]
    [InlineData("https://bc.test/team/dual-tree")]
    [InlineData("https://bc.test/team/dual-changes")]
    public void Las_paginas_del_dual_team_abren_su_grupo(string uri)
        => SidebarRoutes.IsDualTeam(uri).Should().BeTrue();

    [Theory]
    [InlineData("https://bc.test/team/enrollment/my-team")]
    [InlineData("https://bc.test/team/enrollment/customers")]
    [InlineData("https://bc.test/team/enrollment/customer-changes")]
    [InlineData("https://bc.test/team/enrollment/activity")]
    public void Las_paginas_de_inscripcion_abren_su_grupo_y_no_el_dual(string uri)
    {
        SidebarRoutes.IsEnrollmentTeam(uri).Should().BeTrue();
        SidebarRoutes.IsDualTeam(uri).Should().BeFalse();
    }

    [Fact]
    public void Residuales_no_abre_inscripcion_ni_comisiones()
    {
        SidebarRoutes.IsEnrollmentTeam("https://bc.test/team/residuals").Should().BeFalse();
        SidebarRoutes.IsCommissions("https://bc.test/team/residuals").Should().BeFalse();
    }

    [Fact]
    public void Comisiones_abre_su_grupo()
        => SidebarRoutes.IsCommissions("https://bc.test/commissions/boost").Should().BeTrue();
}
