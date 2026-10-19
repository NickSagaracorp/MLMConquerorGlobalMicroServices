using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Jobs;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

public class ApplyDualLegDeltasJobTests
{
    [Fact]
    public async Task En_InMemory_no_hay_cola_y_termina_sin_error()
    {
        await using var db = ReportesDb.Nueva();
        var job = new ApplyDualLegDeltasJob(db, NullLogger<ApplyDualLegDeltasJob>.Instance);
        (await job.ExecuteAsync()).Should().Be(0);
    }
}
