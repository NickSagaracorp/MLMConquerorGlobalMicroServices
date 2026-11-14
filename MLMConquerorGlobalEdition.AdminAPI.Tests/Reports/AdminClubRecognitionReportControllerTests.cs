using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminClubRecognitionReportControllerTests
{
    private static (AdminClubRecognitionReportController C, Mock<IClubRecognitionReportService> S) Montar()
    {
        var servicio = new Mock<IClubRecognitionReportService>();
        return (new AdminClubRecognitionReportController(servicio.Object, new CsvReportWriter(),
            ClubRecognitionTestData.Reloj(new DateTime(2026, 11, 14, 10, 30, 0))), servicio);
    }

    [Fact]
    public async Task La_rejilla_pasa_vista_nivel_y_pedido_al_servicio()
    {
        var (c, s) = Montar();
        var pedido = new GridDataRequest { Search = "ana" };
        s.Setup(x => x.GetPageAsync(
                It.Is<ClubRecognitionFilter>(f => f.View == ClubRecognitionView.NotRecognizedOnline && f.LevelId == 4),
                pedido, It.IsAny<CancellationToken>()))
         .ReturnsAsync(new PagedResult<ClubRecognitionRowView>());

        var r = await c.Grid(pedido, ClubRecognitionView.NotRecognizedOnline, 4, default);

        r.Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task Exportar_da_un_csv_con_las_diez_columnas()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(
                It.Is<ClubRecognitionFilter>(f => f.View == ClubRecognitionView.NotRecognizedOnline && f.LevelId == null
                                               && f.Search == "ana"),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[]
         {
             new ClubRecognitionRowView
             {
                 Id = 1, MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Username = "ana",
                 Country = "Bulgaria", LevelName = "100k Earner", ReachedDate = new DateTime(2026, 11, 13),
                 QualifyingTotal = 55_000m, CeoCallRecognized = null, EventRecognized = "Bootcamp CIS 2026", RewardSize = "US9"
             }
         });

        var r = await c.Export(ClubRecognitionView.NotRecognizedOnline, null, "ana", default);

        var archivo = r.Should().BeOfType<FileContentResult>().Subject;
        archivo.ContentType.Should().Be(CsvReportWriter.ContentType);
        archivo.FileDownloadName.Should().Be("club-recognition-not-recognized-online-2026-11-14.csv");
        Encoding.UTF8.GetString(archivo.FileContents, 3, archivo.FileContents.Length - 3).Should().Be(
            "Ambassador,Email,Username,Country,Level,Reached,Total,CEO Call Recognized,Event Recognized,Reward Size\r\n" +
            "Ana Lopez,ana@example.com,ana,Bulgaria,100k Earner,2026-11-13,55000,,Bootcamp CIS 2026,US9\r\n");
    }

    [Fact]
    public void Leer_y_exportar_es_de_SuperAdmin_y_Admin()
    {
        typeof(AdminClubRecognitionReportController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
        typeof(AdminClubRecognitionReportController).GetMethod(nameof(AdminClubRecognitionReportController.Export))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
    }
}
