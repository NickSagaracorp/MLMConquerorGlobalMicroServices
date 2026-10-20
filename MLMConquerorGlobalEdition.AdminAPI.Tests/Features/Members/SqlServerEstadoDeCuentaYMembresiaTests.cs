using System.Data;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

/// <summary>
/// Plan Q (Q3): el cambio de estado de la cuenta desde el admin contra SQL Server, sin transaccion de quien llama (el
/// camino real): el candado de puntos es de sesion, se toma antes de leer y el guardado lo suelta. Suspender resta los
/// puntos propios del miembro y reactivar los devuelve; al terminar, la conexion queda cerrada.
/// </summary>
public class SqlServerEstadoDeCuentaYMembresiaTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TEC-{Guid.NewGuid():N}"[..20];

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
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET DEADLOCK_PRIORITY HIGH;
                    DELETE FROM SubscriptionBillingStates WHERE MemberId = {m};
                    DELETE FROM DualLegDeltas WHERE StartMemberId = {m} AND OwnChange = 1;
                    DELETE FROM MemberStatisticDeltas WHERE SourceMemberId = {m} OR MemberId = {m};
                    DELETE FROM MemberStatistics WHERE MemberId = {m};
                    DELETE FROM RankEvaluationQueue WHERE TriggerMemberId = {m} OR EvaluateMemberId = {m};
                    DELETE FROM MembershipStatusHistories WHERE MemberId = {m};
                    DELETE FROM MemberStatusHistories WHERE MemberId = {m};
                    DELETE FROM MembershipSubscriptions WHERE MemberId = {m};
                    DELETE FROM MemberProfiles WHERE MemberId = {m};");
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

    private async Task<(MembershipStatus Estado, int Personales)> LeerAsync()
    {
        await using var db = SqlServerTestDb.Db();
        var estado = await db.MembershipSubscriptions.AsNoTracking().Where(s => s.MemberId == _member)
            .Select(s => s.SubscriptionStatus).SingleAsync();
        var puntos = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == _member)
            .Select(s => s.PersonalPoints).FirstOrDefaultAsync();
        return (estado, puntos);
    }

    [SqlServerFact]
    public async Task Suspender_resta_los_puntos_y_reactivar_los_devuelve()
    {
        int puntos;
        await using (var db = SqlServerTestDb.Db())
        {
            puntos = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var ahora = DateTime.Now;
            db.MemberProfiles.Add(new MemberProfile
            {
                MemberId = _member, UserId = Guid.NewGuid(), Email = $"{_member}@example.com", FirstName = "Prueba", LastName = "Q3",
                MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = ahora.AddYears(-1),
                Country = "US", CreatedBy = "plan-q-prueba", CreationDate = ahora, LastUpdateDate = ahora
            });
            db.MembershipSubscriptions.Add(new MembershipSubscription
            {
                MemberId = _member, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
                ChangeReason = SubscriptionChangeReason.New, StartDate = ahora.AddDays(-30), IsAutoRenew = true,
                CreatedBy = "plan-q-prueba", CreationDate = ahora, LastUpdateDate = ahora
            });
            await db.SaveChangesAsync();
        }
        puntos.Should().BeGreaterThan(0);
        (await LeerAsync()).Should().Be((MembershipStatus.Active, puntos));

        await CambiarAsync(MemberAccountStatus.Suspended);
        (await LeerAsync()).Should().Be((MembershipStatus.OnHold, 0));

        await CambiarAsync(MemberAccountStatus.Active);
        (await LeerAsync()).Should().Be((MembershipStatus.Active, puntos));
    }
}
