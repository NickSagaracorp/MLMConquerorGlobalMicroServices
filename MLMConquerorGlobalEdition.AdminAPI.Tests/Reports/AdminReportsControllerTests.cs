using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

[Collection(CacheDeReportesCollection.Nombre)]
public class AdminReportsControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 30, 0);

    private static (AdminReportsController C, Mock<IEarningsRankingService> S) Montar()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var servicio = new Mock<IEarningsRankingService>();
        return (new AdminReportsController(servicio.Object, new CsvReportWriter(), reloj.Object), servicio);
    }

    [Fact]
    public async Task Sin_corte_usa_ayer_con_la_hora_del_servidor()
    {
        var (c, s) = Montar();
        var pedido = new GridDataRequest();
        s.Setup(x => x.GetPageAsync(new DateTime(2026, 11, 13), 180, pedido, It.IsAny<CancellationToken>()))
         .ReturnsAsync(new PagedResult<EarningsRankRowView>());

        var r = await c.EarningsGrid(pedido, cutoff: null, days: 180, default);

        r.Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task Exportar_devuelve_un_csv_con_las_filas_filtradas()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(new DateTime(2026, 10, 31), null, "ana", It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[] { new EarningsRankRowView { Rank = 1, MemberId = "AMB-1", MemberName = "Ana", Total = 5 } });

        var r = await c.EarningsExport(new DateTime(2026, 10, 31), days: null, search: "ana", default);

        var archivo = r.Should().BeOfType<FileContentResult>().Subject;
        archivo.ContentType.Should().Be(CsvReportWriter.ContentType);
        archivo.FileDownloadName.Should().Be("earnings-lifetime-2026-10-31.csv");
        archivo.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public void Los_reportes_son_de_SuperAdmin_y_Admin_y_exportar_tambien()
    {
        typeof(AdminReportsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
        typeof(AdminReportsController).GetMethod(nameof(AdminReportsController.EarningsExport))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
    }
}
