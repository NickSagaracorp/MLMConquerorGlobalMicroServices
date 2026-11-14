using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminRankStandingsReportControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 30, 0);

    private static (AdminRankStandingsReportController C, Mock<IRankReportService> S) Montar()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var servicio = new Mock<IRankReportService>();
        return (new AdminRankStandingsReportController(servicio.Object, new CsvReportWriter(), reloj.Object), servicio);
    }

    [Fact]
    public async Task La_rejilla_pasa_los_filtros_al_servicio()
    {
        var (c, s) = Montar();
        var pedido = new GridDataRequest { Search = "ana" };
        var desde  = new DateTime(2026, 1, 1);
        var hasta  = new DateTime(2026, 11, 14);
        s.Setup(x => x.GetPageAsync(
                It.Is<RankReportFilter>(f => f.Mode == RankReportMode.Current && f.RankDefinitionId == 2
                    && f.Statuses!.SequenceEqual(new[] { MemberAccountStatus.Active })
                    && f.SinceFrom == desde && f.SinceTo == hasta),
                pedido, It.IsAny<CancellationToken>()))
         .ReturnsAsync(new PagedResult<RankReportRowView>());

        var r = await c.Grid(pedido, RankReportMode.Current, 2, new[] { MemberAccountStatus.Active }, desde, hasta, default);

        r.Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task Exportar_da_un_csv_con_las_columnas_del_modo()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(It.Is<RankReportFilter>(f => f.Mode == RankReportMode.Lifetime && f.Search == "ana"),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[]
         {
             new RankReportRowView
             {
                 MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Country = "Bulgaria",
                 Status = MemberAccountStatus.Active, RankDefinitionId = 2, RankName = "Gold", RankSortOrder = 2,
                 Since = new DateTime(2026, 3, 1, 9, 0, 0)
             }
         });

        var r = await c.Export(RankReportMode.Lifetime, null, null, null, null, "ana", default);

        var archivo = r.Should().BeOfType<FileContentResult>().Subject;
        archivo.ContentType.Should().Be(CsvReportWriter.ContentType);
        archivo.FileDownloadName.Should().Be("lifetime-rank-2026-11-14.csv");
        Encoding.UTF8.GetString(archivo.FileContents, 3, archivo.FileContents.Length - 3).Should().Be(
            "Ambassador,Email,Country,Status,Lifetime Rank,Lifetime Rank Since\r\n" +
            "Ana Lopez,ana@example.com,Bulgaria,Active,Gold,2026-03-01\r\n");
    }

    [Fact]
    public async Task Exportar_current_usa_su_nombre_de_archivo()
    {
        var (c, s) = Montar();
        s.Setup(x => x.GetAllAsync(It.IsAny<RankReportFilter>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Array.Empty<RankReportRowView>());

        var r = await c.Export(RankReportMode.Current, null, null, null, null, null, default);

        r.Should().BeOfType<FileContentResult>().Subject.FileDownloadName.Should().Be("current-rank-2026-11-14.csv");
    }

    [Fact]
    public void Leer_y_exportar_es_de_SuperAdmin_y_Admin()
    {
        typeof(AdminRankStandingsReportController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin");
        typeof(AdminRankStandingsReportController).GetMethod(nameof(AdminRankStandingsReportController.Export))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
    }
}
