using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

/// <summary>
/// Plan Q (Q3): el cambio de estado de la cuenta desde el admin contra SQL Server, sin transaccion de quien llama (el
/// camino real): el candado de puntos es de sesion, se toma antes de leer y el guardado lo suelta. Suspender resta los
/// puntos propios del miembro y los de inscripcion de su patrocinador (la linea, revision de Q3, M8), y reactivar los
/// devuelve; al terminar, la conexion queda cerrada.
/// </summary>
public class SqlServerEstadoDeCuentaYMembresiaTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TEC-{Guid.NewGuid():N}"[..20];
    private readonly string _sponsor = $"TEP-{Guid.NewGuid():N}"[..20];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!SqlServerTestDb.Reachable) return;
        for (var intento = 1; ; intento++)
        {
            try
            {
                await using var db = SqlServerTestDb.Db();
                var m = _member;
                var p = _sponsor;
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET DEADLOCK_PRIORITY HIGH;
                    DELETE FROM SubscriptionBillingStates WHERE MemberId = {m};
                    DELETE FROM DualLegDeltas WHERE StartMemberId IN ({m}, {p}) AND OwnChange = 1;
                    DELETE FROM MemberStatisticDeltas WHERE SourceMemberId IN ({m}, {p}) OR MemberId IN ({m}, {p});
                    DELETE FROM MemberStatistics WHERE MemberId IN ({m}, {p});
                    DELETE FROM RankEvaluationQueue WHERE TriggerMemberId IN ({m}, {p}) OR EvaluateMemberId IN ({m}, {p});
                    DELETE FROM MembershipStatusHistories WHERE MemberId = {m};
                    DELETE FROM MemberStatusHistories WHERE MemberId = {m};
                    DELETE FROM MembershipSubscriptions WHERE MemberId = {m};
                    DELETE FROM GenealogyTree WHERE MemberId IN ({m}, {p});
                    DELETE FROM MemberProfiles WHERE MemberId IN ({m}, {p});");
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 1205 or -2 && intento < 8)
            {
                await Task.Delay(500 * intento);
            }
        }
    }

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private async Task CambiarAsync(MemberAccountStatus estado)
    {
        await using var db = SqlServerTestDb.Db();
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("admin-prueba");
        var r = await new UpdateMemberStatusHandler(db, usuario.Object, new Reloj(), new NoOpCacheService())
            .Handle(new UpdateMemberStatusCommand(_member, new UpdateMemberStatusRequest { Status = estado }), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        db.Database.GetDbConnection().State.Should().Be(ConnectionState.Closed, "el guardado suelta el candado y cierra la conexion");
    }

    /// <summary>
    /// El estado de la suscripcion, los puntos propios del miembro y los de inscripcion del patrocinador con lo encolado de
    /// esta linea aplicado. La cola se vacia dentro de una transaccion que se deshace (con lo ajeno marcado como aplicado,
    /// como SqlServerDevolucionYEstadoDeCuentaTests): lo de esta linea sigue pendiente y cada lectura lo aplica entero.
    /// </summary>
    private async Task<(MembershipStatus Estado, int Personales, int InscripcionDelPatrocinador)> LeerAsync()
    {
        await using var db = SqlServerTestDb.Db();
        var estado = await db.MembershipSubscriptions.AsNoTracking().Where(s => s.MemberId == _member)
            .Select(s => s.SubscriptionStatus).SingleAsync();
        var puntos = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == _member)
            .Select(s => s.PersonalPoints).FirstOrDefaultAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        var m = _member;
        var p = _sponsor;
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE MemberStatisticDeltas SET IsApplied = 1
            WHERE IsApplied = 0 AND SourceMemberId NOT IN ({m}, {p}) AND MemberId NOT IN ({m}, {p});");
        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();
        db.ChangeTracker.Clear();
        var inscripcion = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == _sponsor)
            .OrderBy(s => s.Id).Select(s => s.EnrollmentPoints).FirstOrDefaultAsync();
        await tx.RollbackAsync();
        return (estado, puntos, inscripcion);
    }

    [SqlServerFact]
    public async Task Suspender_resta_los_puntos_del_miembro_y_de_su_patrocinador_y_reactivar_los_devuelve()
    {
        int puntos;
        await using (var db = SqlServerTestDb.Db())
        {
            puntos = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var ahora = DateTime.Now;
            foreach (var (id, patrocinador) in new[] { (_sponsor, (string?)null), (_member, _sponsor) })
                db.MemberProfiles.Add(new MemberProfile
                {
                    MemberId = id, UserId = Guid.NewGuid(), Email = $"{id}@example.com", FirstName = "Prueba", LastName = "Q3",
                    MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = ahora.AddYears(-1),
                    SponsorMemberId = patrocinador, Country = "US", CreatedBy = "plan-q-prueba", CreationDate = ahora,
                    LastUpdateDate = ahora
                });
            // Un patrocinador raiz con un ordinal alto al azar (no choca con los nodos de la base local) y el miembro debajo.
            var raiz = HierarchyId.Parse($"/{Random.Shared.Next(1_000_000, int.MaxValue)}/");
            db.GenealogyTree.Add(new GenealogyEntity
            {
                MemberId = _sponsor, HierarchyPath = $"/{_sponsor}/", Level = 0, Node = raiz, CreatedBy = "plan-q-prueba"
            });
            db.GenealogyTree.Add(new GenealogyEntity
            {
                MemberId = _member, ParentMemberId = _sponsor, HierarchyPath = $"/{_sponsor}/{_member}/", Level = 1,
                Node = raiz.GetDescendant(null, null), CreatedBy = "plan-q-prueba"
            });
            db.MemberStatistics.Add(new MemberStatisticEntity { MemberId = _sponsor, CreatedBy = "plan-q-prueba" });
            await db.SaveChangesAsync();

            db.MembershipSubscriptions.Add(new MembershipSubscription
            {
                MemberId = _member, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
                ChangeReason = SubscriptionChangeReason.New, StartDate = ahora.AddDays(-30), IsAutoRenew = true,
                CreatedBy = "plan-q-prueba", CreationDate = ahora, LastUpdateDate = ahora
            });
            await db.SaveChangesAsync();
        }
        puntos.Should().BeGreaterThan(0);
        (await LeerAsync()).Should().Be((MembershipStatus.Active, puntos, puntos));

        await CambiarAsync(MemberAccountStatus.Suspended);
        (await LeerAsync()).Should().Be((MembershipStatus.OnHold, 0, 0));

        await CambiarAsync(MemberAccountStatus.Active);
        (await LeerAsync()).Should().Be((MembershipStatus.Active, puntos, puntos));
    }
}
