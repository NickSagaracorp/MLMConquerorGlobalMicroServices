using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Trees;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Tarea 7: dos colocaciones manuales del BizCenter al mismo hueco a la vez, cada una con su
/// contexto. La comprobacion de hueco libre estaba fuera de la transaccion y sin candado: las dos la pasaban.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColocacionManualConcurrenteTests
{
    private static PlaceMemberHandler Handler(AppDbContext db)
    {
        var usuario = new Mock<BizCenter.Services.ICurrentUserService>();
        usuario.Setup(u => u.IsAdmin).Returns(true); // sin ventana ni oportunidades: solo la estructura
        usuario.Setup(u => u.UserId).Returns("plan-p-prueba");
        var reloj = new Mock<BizCenter.Services.IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(DateTime.Now);
        reloj.Setup(r => r.UtcNow).Returns(DateTime.UtcNow);
        var piernas = new Mock<IDualTeamPointsRecalculator>();
        piernas.Setup(p => p.RecalculateForUplinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return new PlaceMemberHandler(db, usuario.Object, reloj.Object, piernas.Object);
    }

    [SqlServerFact]
    public async Task Dos_colocaciones_manuales_al_mismo_hueco_dejan_un_hijo()
    {
        var x = BinarioDb.Prefijo("PPB");
        var encuentro = new Encuentro();
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left));
                await BinarioDb.SembrarPerfilesAsync(db, $"{x}M1", $"{x}M2");
            }

            var resultados = await Task.WhenAll(new[] { $"{x}M1", $"{x}M2" }.Select(async m =>
            {
                await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(SqlServerReportes.ConnectionString).AddInterceptors(encuentro).Options);
                try
                {
                    return await Handler(db).Handle(new PlaceMemberCommand(m, $"{x}P", "Left"), CancellationToken.None);
                }
                catch (DbUpdateException ex)
                {
                    return MLMConquerorGlobalEdition.SharedKernel.Result<PlaceMemberResult>.Failure("DB", ex.InnerException?.Message ?? ex.Message);
                }
            }));

            resultados.Count(r => r.IsSuccess).Should().Be(1);
            resultados.Single(r => !r.IsSuccess).ErrorCode.Should().Be("SLOT_OCCUPIED");
            await using var lectura = SqlServerReportes.Db();
            (await BinarioDb.HijosAsync(lectura, $"{x}P", TreeSide.Left)).Should().Be(1);
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }
}
