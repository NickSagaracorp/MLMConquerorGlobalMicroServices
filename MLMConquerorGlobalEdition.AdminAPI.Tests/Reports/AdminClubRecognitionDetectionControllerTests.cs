using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminClubRecognitionDetectionControllerTests
{
    [Fact]
    public async Task La_carga_manual_llega_hasta_ayer_en_hora_del_servidor()
    {
        var detector = new Mock<IClubRecognitionDetector>();
        detector.Setup(d => d.LoadHistoryAsync(new DateTime(2026, 11, 13), It.IsAny<CancellationToken>())).ReturnsAsync(42);
        var c = new AdminClubRecognitionDetectionController(detector.Object,
            ClubRecognitionTestData.Reloj(new DateTime(2026, 11, 14, 9, 30, 0)));

        var r = await c.LoadHistory(default);

        var datos = ((ApiResponse<AdminClubRecognitionDetectionController.HistoryResult>)
            r.Should().BeOfType<OkObjectResult>().Subject.Value!).Data!;
        datos.Should().Be(new AdminClubRecognitionDetectionController.HistoryResult(new DateTime(2026, 11, 13), 42));
    }

    [Fact]
    public void Solo_SuperAdmin()
        => typeof(AdminClubRecognitionDetectionController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin");
}
