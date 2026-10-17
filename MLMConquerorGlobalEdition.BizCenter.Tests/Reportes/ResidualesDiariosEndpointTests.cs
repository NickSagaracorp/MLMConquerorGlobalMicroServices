using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class ResidualesDiariosEndpointTests
{
    [Fact]
    public async Task El_BizCenter_pide_el_historial_del_miembro_del_token()
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        var servicio = new Mock<IDailyResidualReportService>();
        servicio.Setup(s => s.GetHistoryAsync("AMB-000777", 2026, 3, 2, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DailyResidualHistoryView());
        var control = new CommissionsController(new Mock<IMediator>().Object, new Mock<IDownlineGuard>().Object);

        var respuesta = await control.GetDailyResiduals(servicio.Object, usuario.Object, 2026, 3, 2, 10, CancellationToken.None);

        respuesta.Should().BeOfType<OkObjectResult>();
        servicio.Verify(s => s.GetHistoryAsync("AMB-000777", 2026, 3, 2, 10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
