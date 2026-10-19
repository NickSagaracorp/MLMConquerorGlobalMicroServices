using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Escala;

/// <summary>
/// Plan P, Bloque D: el progreso de rango completo (GetRankProgressHandler) de ESC-M en la base de escala, con 400.000
/// o 1.000.000 en sus dos equipos. Mediana de 3 ejecuciones; objetivo menos de 1 s (Plan Q), con el tiempo de espera de
/// comando de produccion (30 s). Solo con MLM_ESCALA_SQLSERVER.
/// </summary>
[Trait("Categoria", "Medicion")]
public class EscalaProgresoDeRangoTests
{
    private readonly ITestOutputHelper _salida;
    public EscalaProgresoDeRangoTests(ITestOutputHelper salida) => _salida = salida;

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [EscalaFact]
    public async Task El_progreso_de_rango_de_ESC_M_responde_en_tiempo()
    {
        await using var db = EscalaDb.Db(30);
        var handler = new GetRankProgressHandler(db, new Reloj(), new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db));
        var t = await EscalaDb.MedianaAsync(_salida, "Progreso de rango de ESC-M (completo)", async _ =>
        {
            var r = await handler.Handle(new GetRankProgressQuery("ESC-M"), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error);
        });
        t.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
