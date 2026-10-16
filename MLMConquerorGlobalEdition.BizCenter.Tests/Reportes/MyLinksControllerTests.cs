using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.Repository.Services.Links;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class MyLinksControllerTests
{
    [Fact]
    public async Task Pide_los_enlaces_del_miembro_del_token()
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-000777");
        var enlaces = new Mock<IMyLinksService>();
        enlaces.Setup(s => s.GetAsync("AMB-000777", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MyLinksView());

        var respuesta = await new MyLinksController(enlaces.Object, usuario.Object).Get(CancellationToken.None);

        respuesta.Should().BeOfType<OkObjectResult>();
        enlaces.Verify(s => s.GetAsync("AMB-000777", It.IsAny<CancellationToken>()), Times.Once);
    }
}
