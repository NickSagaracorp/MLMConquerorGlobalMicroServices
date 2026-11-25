using System.Globalization;
using System.Reflection;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>El job de los lunes escribe la semana que cerró; al arrancar solo la escribe si falta.</summary>
public class ReportSnapshotJobTests
{
    private static readonly DateTime Lunes   = DateTime.Parse("2026-11-23T01:15:00", CultureInfo.InvariantCulture);
    private static readonly DateTime Domingo = new(2026, 11, 22);

    private static ReportSnapshotJob Job(Mock<IReportSnapshotWriter> escritor)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Lunes);
        return new ReportSnapshotJob(escritor.Object, reloj.Object, NullLogger<ReportSnapshotJob>.Instance);
    }

    [Fact]
    public async Task Escribe_el_domingo_que_cerro()
    {
        var escritor = new Mock<IReportSnapshotWriter>();
        escritor.Setup(e => e.WriteAsync(Domingo, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportSnapshotResult(1, 2, 3, 4));

        await Job(escritor).ExecuteAsync(CancellationToken.None);

        escritor.VerifyAll();
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task Al_arrancar_solo_la_escribe_si_falta(bool existe, int escrituras)
    {
        var escritor = new Mock<IReportSnapshotWriter>();
        escritor.Setup(e => e.ExistsAsync(Domingo, It.IsAny<CancellationToken>())).ReturnsAsync(existe);
        escritor.Setup(e => e.WriteAsync(Domingo, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportSnapshotResult(0, 0, 0, 0));

        await Job(escritor).ExecuteIfMissingAsync(CancellationToken.None);

        escritor.Verify(e => e.WriteAsync(Domingo, It.IsAny<CancellationToken>()), Times.Exactly(escrituras));
    }

    [Fact]
    public void Va_en_la_cola_del_bizcenter()
        => typeof(ReportSnapshotJob).GetCustomAttribute<QueueAttribute>()!.Queue.Should().Be("bizcenter");
}
