using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.DowngradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// Plan Q (seguimiento de la tercera revision del bloque 2): dos cambios de nivel del mismo miembro a la vez, cada uno con
/// su contexto, contra SQL Server. Leian la suscripcion activa antes del candado por miembro; los dos caducaban la misma y
/// cada uno abria una nueva: dos activas. Una barrera en SavingChanges deja que los dos lleguen a guardar antes de que
/// ninguno confirme (si pueden). Con el candado tomado antes de leer, el segundo lee lo que confirmo el primero: el
/// resultado es el de hacerlos uno detras de otro y queda UNA activa.
/// </summary>
public class SqlServerCambiosDeNivelConcurrentesTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TCN-{Guid.NewGuid():N}"[..20];

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Cada guardado espera (como mucho 3 s) a que el otro llegue tambien a guardar.</summary>
    private sealed class Barrera : SaveChangesInterceptor
    {
        private int _llegados;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _llegados);
            var hasta = DateTime.Now.AddSeconds(3);
            while (Volatile.Read(ref _llegados) < 2 && DateTime.Now < hasta)
                await Task.Delay(50, cancellationToken);
            return result;
        }
    }

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

    private async Task SembrarAsync(int nivel)
    {
        await using var db = SqlServerTestDb.Db();
        var ahora = DateTime.Now;
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = _member, UserId = Guid.NewGuid(), Email = $"{_member}@example.com", FirstName = "Prueba", LastName = "PlanQ",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = ahora.AddYears(-1),
            Country = "US", CreatedBy = "plan-q-prueba", CreationDate = ahora, LastUpdateDate = ahora
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = _member, MembershipLevelId = nivel, SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New, StartDate = ahora.AddDays(-30), IsAutoRenew = true,
            CreatedBy = "plan-q-prueba", CreationDate = ahora, LastUpdateDate = ahora
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Los niveles activos de la base, del menor al mayor SortOrder.</summary>
    private static async Task<List<int>> NivelesAsync()
    {
        await using var db = SqlServerTestDb.Db();
        return await db.MembershipLevels.AsNoTracking().Where(l => l.IsActive && !l.IsFree)
            .OrderBy(l => l.SortOrder).Select(l => l.Id).ToListAsync();
    }

    /// <summary>Un cambio que el otro dejo sin sentido (mismo nivel, o ya no es subida o bajada) falla como siempre.</summary>
    private static async Task<Result<bool>?> IntentarAsync(Func<Task<Result<bool>>> cambio)
    {
        try { return await cambio(); }
        catch (MembershipChangeNotAllowedException) { return null; }
    }

    private async Task<int> ActivasAsync()
    {
        await using var db = SqlServerTestDb.Db();
        return await db.MembershipSubscriptions.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(s => s.MemberId == _member && s.SubscriptionStatus == MembershipStatus.Active && !s.IsDeleted);
    }

    [SqlServerFact]
    public async Task Dos_subidas_a_la_vez_del_mismo_miembro_dejan_una_sola_activa()
    {
        var niveles = await NivelesAsync();
        niveles.Count.Should().BeGreaterThanOrEqualTo(3, "hacen falta tres niveles de pago activos");
        await SembrarAsync(niveles[0]);

        var barrera = new Barrera();
        await using var ca = SqlServerTestDb.Db(barrera);
        await using var cb = SqlServerTestDb.Db(barrera);
        var a = IntentarAsync(() => new UpgradeMembershipHandler(ca, new Reloj(), SubidaDeNivel.Encendida())
            .Handle(new UpgradeMembershipCommand(_member, niveles[1], null), CancellationToken.None));
        var b = IntentarAsync(() => new UpgradeMembershipHandler(cb, new Reloj(), SubidaDeNivel.Encendida())
            .Handle(new UpgradeMembershipCommand(_member, niveles[2], null), CancellationToken.None));
        await Task.WhenAll(a, b);

        (await ActivasAsync()).Should().Be(1, "dos subidas a la vez no pueden dejar dos suscripciones activas");
    }

    [SqlServerFact]
    public async Task Una_subida_y_una_bajada_a_la_vez_del_mismo_miembro_dejan_una_sola_activa()
    {
        var niveles = await NivelesAsync();
        niveles.Count.Should().BeGreaterThanOrEqualTo(3, "hacen falta tres niveles de pago activos");
        await SembrarAsync(niveles[1]);

        var barrera = new Barrera();
        await using var ca = SqlServerTestDb.Db(barrera);
        await using var cb = SqlServerTestDb.Db(barrera);
        var a = IntentarAsync(() => new UpgradeMembershipHandler(ca, new Reloj(), SubidaDeNivel.Encendida())
            .Handle(new UpgradeMembershipCommand(_member, niveles[2], null), CancellationToken.None));
        var b = IntentarAsync(() => new DowngradeMembershipHandler(cb, new Reloj())
            .Handle(new DowngradeMembershipCommand(_member, niveles[0], null), CancellationToken.None));
        await Task.WhenAll(a, b);

        (await ActivasAsync()).Should().Be(1, "una subida y una bajada a la vez no pueden dejar dos suscripciones activas");
    }
}
