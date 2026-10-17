using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentCustomers;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class ClientesEndpointTests
{
    [Theory]
    [InlineData("personal", CustomerScope.Personal)]
    [InlineData(null,       CustomerScope.Team)]
    public async Task La_rejilla_pide_los_clientes_del_miembro_del_token_con_el_alcance_de_la_query(
        string? scope, CustomerScope esperado)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        var inscripcion = new Mock<IEnrollmentTeamService>();
        inscripcion.Setup(s => s.GetCustomersGridAsync("AMB-000777", It.IsAny<GridDataRequest>(), esperado, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new PagedResult<EnrollmentCustomerView>());
        var control = new TeamController(new Mock<IMediator>().Object, new Mock<IDualTeamService>().Object,
            inscripcion.Object, usuario.Object, new Mock<IDownlineGuard>().Object);

        var respuesta = await control.GetEnrollmentCustomersGrid(new GridDataRequest(), scope, CancellationToken.None);

        respuesta.Should().BeOfType<OkObjectResult>();
        inscripcion.Verify(s => s.GetCustomersGridAsync("AMB-000777", It.IsAny<GridDataRequest>(), esperado, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_GET_antiguo_devuelve_el_nivel_y_deja_el_correo_ajeno_vacio()
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        var inscripcion = new Mock<IEnrollmentTeamService>();
        inscripcion.Setup(s => s.GetCustomersAsync("AMB-000777", 1, 20, null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new PagedResult<EnrollmentCustomerView>
                   {
                       Items = new List<EnrollmentCustomerView>
                       {
                           new() { MemberId = "MBR-1", Level = 3, Email = null, IsPersonal = false }
                       }
                   });

        var r = await new GetEnrollmentCustomersHandler(inscripcion.Object, usuario.Object)
            .Handle(new GetEnrollmentCustomersQuery(), CancellationToken.None);

        var fila = r.Value!.Items.Single();
        (fila.Level, fila.Email).Should().Be((3, (string?)null));
    }
}
