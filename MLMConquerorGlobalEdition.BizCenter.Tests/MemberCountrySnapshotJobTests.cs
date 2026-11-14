using System.Globalization;
using System.Reflection;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using IDateTimeProvider = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>El job escribe el día que acaba de cerrar en hora del servidor, sea cual sea la hora a la que corre.</summary>
public class MemberCountrySnapshotJobTests
{
    [Theory]
    [InlineData("2026-11-14T00:30:00", "2026-11-13")]   // invierno: 05:30 UTC = 00:30 local
    [InlineData("2026-07-14T01:30:00", "2026-07-13")]   // verano: 05:30 UTC = 01:30 local
    [InlineData("2026-12-01T00:30:00", "2026-11-30")]   // cambio de mes
    public async Task Escribe_el_dia_que_cerro(string ahora, string esperado)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(DateTime.Parse(ahora, CultureInfo.InvariantCulture));
        var escritor = new Mock<IMemberCountrySnapshotWriter>();
        escritor.Setup(e => e.WriteAsync(DateTime.Parse(esperado, CultureInfo.InvariantCulture), It.IsAny<CancellationToken>()))
                .ReturnsAsync(12);

        await new MemberCountrySnapshotJob(escritor.Object, reloj.Object, NullLogger<MemberCountrySnapshotJob>.Instance)
            .ExecuteAsync(CancellationToken.None);

        escritor.VerifyAll();
    }

    [Fact]
    public void Va_en_la_cola_del_bizcenter()
        => typeof(MemberCountrySnapshotJob).GetCustomAttribute<QueueAttribute>()!.Queue.Should().Be("bizcenter");
}
