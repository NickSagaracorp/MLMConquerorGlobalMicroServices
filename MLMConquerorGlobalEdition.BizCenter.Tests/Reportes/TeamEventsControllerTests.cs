using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class TeamEventsControllerTests
{
    private static TeamEventsController Control(ITeamEventRegistrationsService servicio)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        return new TeamEventsController(servicio, usuario.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task La_rejilla_es_del_equipo_del_token()
    {
        var servicio = new Mock<ITeamEventRegistrationsService>();
        servicio.Setup(s => s.GetGridAsync("AMB-000777", "EV-1", false, It.IsAny<GridDataRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PagedResult<TeamEventRegistrationView>>.Success(new PagedResult<TeamEventRegistrationView>()));

        var r = await Control(servicio.Object).GetTeamRegistrationsGrid("EV-1", new GridDataRequest(), registeredOnly: false);

        r.Should().BeOfType<OkObjectResult>();
        servicio.VerifyAll();
    }

    [Fact]
    public async Task Un_evento_que_no_se_ve_es_404()
    {
        var servicio = new Mock<ITeamEventRegistrationsService>();
        servicio.Setup(s => s.GetTotalsAsync("AMB-000777", "EV-OFF", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TeamEventTotalsView>.Failure(TeamEventErrors.EventNotFound, "Event not found."));

        var r = await Control(servicio.Object).GetTeamRegistrationsTotals("EV-OFF");

        r.Should().BeOfType<NotFoundObjectResult>();
    }
}
