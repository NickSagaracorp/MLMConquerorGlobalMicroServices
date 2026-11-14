using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class AdminTopCountriesReportControllerTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 10, 30, 0);
    private static readonly DateTime Hoy   = new(2026, 11, 14);

    private static (AdminTopCountriesReportController C, Mock<ITopCountriesReportService> S,
        Mock<IMemberCountrySnapshotWriter> W) Montar()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        var servicio = new Mock<ITopCountriesReportService>();
        var escritor = new Mock<IMemberCountrySnapshotWriter>();
        return (new AdminTopCountriesReportController(servicio.Object, escritor.Object, new CsvReportWriter(), reloj.Object),
            servicio, escritor);
    }

    private static async Task<IReadOnlyList<ReportPeriod>> Periodos(CountryChartFrequency frecuencia,
        DateTime? desde = null, DateTime? hasta = null, DateTime? fecha = null)
    {
        var (c, s, _) = Montar();
        IReadOnlyList<ReportPeriod>? periodos = null;
        s.Setup(x => x.GetChartAsync(It.IsAny<IReadOnlyList<ReportPeriod>>(),
                It.IsAny<IReadOnlyCollection<MemberAccountStatus>?>(), It.IsAny<CancellationToken>()))
         .Callback<IReadOnlyList<ReportPeriod>, IReadOnlyCollection<MemberAccountStatus>?, CancellationToken>(
             (p, _, _) => periodos = p)
         .ReturnsAsync(Array.Empty<CountryChartPointView>());

        (await c.Chart(frecuencia, desde, hasta, fecha, null, default)).Should().BeOfType<OkObjectResult>();
        return periodos!;
    }

    [Fact]
    public async Task La_tabla_sin_fecha_usa_hoy_del_servidor()
    {
        var (c, s, _) = Montar();
        s.Setup(x => x.GetTableAsync(Hoy, It.IsAny<IReadOnlyCollection<MemberAccountStatus>?>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TopCountriesTableView());

        (await c.Table(null, null, default)).Should().BeOfType<OkObjectResult>();
        s.VerifyAll();
    }

    [Fact]
    public async Task La_grafica_mensual_por_defecto_son_los_ultimos_12_meses()
    {
        var p = await Periodos(CountryChartFrequency.Monthly);

        p.Should().HaveCount(12);
        (p[0].Label, p[^1].Label).Should().Be(("Dec 2025", "Nov 2026"));
    }

    [Fact]
    public async Task La_grafica_semanal_son_las_semanas_del_mes_de_la_fecha()
        => (await Periodos(CountryChartFrequency.Weekly, fecha: new DateTime(2026, 11, 20))).Select(x => x.Label)
            .Should().Equal("Nov 1", "Nov 2-8", "Nov 9-15", "Nov 16-22", "Nov 23-29", "Nov 30");

    [Fact]
    public async Task La_grafica_diaria_son_los_dias_de_la_semana_cortada_por_el_mes()
        => (await Periodos(CountryChartFrequency.Daily, fecha: new DateTime(2026, 11, 30))).Select(x => x.Label)
            .Should().Equal("Mon Nov 30");

    [Fact]
    public async Task Un_rango_invertido_da_400()
    {
        var (c, _, _) = Montar();

        var r = await c.Chart(CountryChartFrequency.Monthly, new DateTime(2026, 11, 1), new DateTime(2026, 10, 1), null, null, default);

        r.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Exportar_da_un_csv_de_la_tabla_con_la_fecha_de_la_foto()
    {
        var (c, s, _) = Montar();
        s.Setup(x => x.GetTableAsync(Hoy, It.IsAny<IReadOnlyCollection<MemberAccountStatus>?>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TopCountriesTableView
         {
             SnapshotDate = new DateTime(2026, 11, 13),
             Rows = new[]
             {
                 new CountryRowView { Country = "US", Total = 19, Ambassadors = 14, Customers = 5, Change = 5 },
                 new CountryRowView { Country = "Peru", Total = 1, Ambassadors = 1, Customers = 0, Change = null }
             }
         });

        var archivo = (await c.Export(null, null, default)).Should().BeOfType<FileContentResult>().Subject;

        archivo.FileDownloadName.Should().Be("top-countries-2026-11-13.csv");
        Encoding.UTF8.GetString(archivo.FileContents, 3, archivo.FileContents.Length - 3).Should().Be(
            "Country,Total,Ambassadors,Customers,Change\r\nUS,19,14,5,5\r\nPeru,1,1,0,\r\n");
    }

    [Fact]
    public async Task La_foto_manual_escribe_hoy()
    {
        var (c, _, w) = Montar();
        w.Setup(x => x.WriteAsync(Hoy, It.IsAny<CancellationToken>())).ReturnsAsync(8);

        (await c.TakeSnapshot(default)).Should().BeOfType<OkObjectResult>();
        w.VerifyAll();
    }

    [Fact]
    public void Leer_y_exportar_es_de_SuperAdmin_y_Admin_y_la_foto_manual_solo_de_SuperAdmin()
    {
        var tipo = typeof(AdminTopCountriesReportController);
        tipo.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
        tipo.GetMethod(nameof(AdminTopCountriesReportController.Export))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin");
        tipo.GetMethod(nameof(AdminTopCountriesReportController.TakeSnapshot))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin");
    }
}
