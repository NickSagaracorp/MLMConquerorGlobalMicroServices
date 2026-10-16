using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class TeamReportsControllerTests
{
    private static readonly DateTime Desde = new(2026, 10, 1);
    private static readonly DateTime Hasta = new(2026, 10, 16);

    internal static TeamReportsController Control(string memberId = "AMB-000777")
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns(memberId);
        return new TeamReportsController(usuario.Object, new RelojFijo(new DateTime(2026, 10, 16, 15, 0, 0)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public void El_setup_sale_del_reloj_del_servidor()
    {
        var r = Control().GetSetup().Should().BeOfType<OkObjectResult>().Subject;

        var setup = ((ApiResponse<ReportSetupView>)r.Value!).Data!;
        setup.DefaultFrom.Should().Be(new DateTime(2026, 10, 1));
        setup.DefaultTo.Should().Be(new DateTime(2026, 10, 16));
        setup.ActivityMinFrom.Should().Be(new DateTime(2026, 7, 16));
        setup.MaxRangeDays.Should().Be(366);
    }

    [Fact]
    public async Task La_actividad_es_la_del_miembro_del_token()
    {
        var actividad = new Mock<IEnrollmentActivityService>();
        actividad.Setup(s => s.GetGridAsync("AMB-000777", Desde, Hasta, It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PagedResult<EnrollmentActivityRowView>>.Success(new PagedResult<EnrollmentActivityRowView>()));

        var r = await Control().GetActivityGrid(new GridDataRequest(), Desde, Hasta, actividad.Object, CancellationToken.None);

        r.Should().BeOfType<OkObjectResult>();
        actividad.VerifyAll();
    }

    [Fact]
    public async Task Un_rango_invalido_es_400_con_su_codigo()
    {
        var actividad = new Mock<IEnrollmentActivityService>();
        actividad.Setup(s => s.GetGridAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PagedResult<EnrollmentActivityRowView>>.Failure(ReportDateRange.TooOldCode, "Invalid date range."));

        var r = await Control().GetActivityGrid(new GridDataRequest(), Desde, Hasta, actividad.Object, CancellationToken.None);

        var bad = r.Should().BeOfType<BadRequestObjectResult>().Subject;
        ((ApiResponse<PagedResult<EnrollmentActivityRowView>>)bad.Value!).ErrorCode.Should().Be(ReportDateRange.TooOldCode);
    }
}
