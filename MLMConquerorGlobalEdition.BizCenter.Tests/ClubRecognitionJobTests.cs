using System.Globalization;
using System.Reflection;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El job evalúa el día que cerró en hora del servidor. La primera vez (sin marcador) hace la carga histórica hasta ese
/// día; después, la detección nocturna. Nunca las dos en la misma corrida.
/// </summary>
public class ClubRecognitionJobTests
{
    private static (ClubRecognitionJob Job, Mock<IClubRecognitionDetector> Detector) Montar(string ahora, bool cargada)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(DateTime.Parse(ahora, CultureInfo.InvariantCulture));
        var detector = new Mock<IClubRecognitionDetector>();
        detector.Setup(d => d.HistoryLoadedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cargada);
        return (new ClubRecognitionJob(detector.Object, reloj.Object, NullLogger<ClubRecognitionJob>.Instance), detector);
    }

    [Theory]
    [InlineData("2026-11-14T00:45:00", "2026-11-13")]   // invierno: 05:45 UTC = 00:45 local
    [InlineData("2026-07-14T01:45:00", "2026-07-13")]   // verano: 05:45 UTC = 01:45 local
    [InlineData("2026-12-01T00:45:00", "2026-11-30")]   // cambio de mes
    public async Task Con_la_carga_hecha_detecta_el_dia_que_cerro(string ahora, string esperado)
    {
        var (job, detector) = Montar(ahora, cargada: true);
        var dia = DateTime.Parse(esperado, CultureInfo.InvariantCulture);
        detector.Setup(d => d.DetectDayAsync(dia, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        await job.ExecuteAsync(CancellationToken.None);

        detector.Verify(d => d.DetectDayAsync(dia, It.IsAny<CancellationToken>()), Times.Once);
        detector.Verify(d => d.LoadHistoryAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_la_carga_hecha_hace_la_historica_hasta_el_dia_que_cerro_y_no_la_nocturna()
    {
        var (job, detector) = Montar("2026-11-14T00:45:00", cargada: false);
        detector.Setup(d => d.LoadHistoryAsync(new DateTime(2026, 11, 13), It.IsAny<CancellationToken>())).ReturnsAsync(812);

        await job.ExecuteAsync(CancellationToken.None);

        detector.Verify(d => d.LoadHistoryAsync(new DateTime(2026, 11, 13), It.IsAny<CancellationToken>()), Times.Once);
        detector.Verify(d => d.DetectDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Va_en_la_cola_del_bizcenter()
        => typeof(ClubRecognitionJob).GetCustomAttribute<QueueAttribute>()!.Queue.Should().Be("bizcenter");
}
