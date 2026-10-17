using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Tests.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// El esquema de la cuota anual contra SQL Server (Plan K, K1, K2; revisión M7). InMemory no aplica
/// índices únicos filtrados ni CHECK: solo aquí se ve que la base impide un segundo estado de cuota del
/// mismo miembro, un estado de cuota con suscripción, uno de membresía sin ella y un segundo plan de
/// cuota. Usa ids sintéticos propios y borra lo que escribe.
/// </summary>
public class SqlServerEsquemaCuotaAnualTests : IAsyncLifetime
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private readonly string _prefijo = $"TSQ-{Guid.NewGuid():N}"[..16];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!SqlServerTestDb.Reachable) return;
        await using var db = SqlServerTestDb.Db();
        var p = _prefijo + "%";
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            SET DEADLOCK_PRIORITY HIGH;
            DELETE FROM SubscriptionBillingStates WHERE MemberId LIKE {p};
            DELETE FROM RecurringBillingPlans WHERE Name LIKE {p};");
    }

    private static async Task<int> PlanAnualAsync(AppDbContext db) =>
        await db.RecurringBillingPlans.Where(p => p.IsAnnualFeePlan).Select(p => p.Id).SingleAsync();

    private SubscriptionBillingState Estado(int planId, string sufijo, BillingStatePurpose proposito, string? suscripcion) => new()
    {
        Purpose = proposito, MembershipSubscriptionId = suscripcion, MemberId = _prefijo + sufijo,
        RecurringBillingPlanId = planId, BillingAnchorDate = Ahora.AddYears(-1), NextBillingDate = Ahora.AddYears(1),
        NextAttemptDate = Ahora.AddYears(1), Status = RecurringBillingStatus.Active, CreatedBy = "sql-test",
        CreationDate = Ahora, LastUpdateDate = Ahora
    };

    private static int NumeroSql(DbUpdateException ex) => (ex.InnerException as SqlException)?.Number ?? 0;

    [SqlServerFact]
    public async Task Un_miembro_no_puede_tener_dos_estados_de_cuota()
    {
        await using var db = SqlServerTestDb.Db();
        var plan = await PlanAnualAsync(db);
        db.SubscriptionBillingStates.Add(Estado(plan, "A", BillingStatePurpose.AnnualFee, null));
        await db.SaveChangesAsync();

        await using var otra = SqlServerTestDb.Db();
        otra.SubscriptionBillingStates.Add(Estado(plan, "A", BillingStatePurpose.AnnualFee, null));
        var act = () => otra.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should()
            .Match<DbUpdateException>(e => NumeroSql(e) == 2601 || NumeroSql(e) == 2627);
    }

    [SqlServerFact]
    public async Task Un_estado_de_cuota_con_suscripcion_lo_rechaza_el_CHECK()
    {
        await using var db = SqlServerTestDb.Db();
        db.SubscriptionBillingStates.Add(Estado(await PlanAnualAsync(db), "B", BillingStatePurpose.AnnualFee, _prefijo + "-sub"));
        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().Match<DbUpdateException>(e => NumeroSql(e) == 547);
    }

    [SqlServerFact]
    public async Task Un_estado_de_membresia_sin_suscripcion_lo_rechaza_el_CHECK()
    {
        await using var db = SqlServerTestDb.Db();
        db.SubscriptionBillingStates.Add(Estado(1, "C", BillingStatePurpose.Membership, null));
        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().Match<DbUpdateException>(e => NumeroSql(e) == 547);
    }

    [SqlServerFact]
    public async Task Dos_estados_de_membresia_de_un_mismo_miembro_si_caben()
    {
        // El índice de cuota está filtrado a Purpose = 2: no afecta a las membresías.
        await using var db = SqlServerTestDb.Db();
        db.SubscriptionBillingStates.Add(Estado(1, "D", BillingStatePurpose.Membership, _prefijo + "-s1"));
        db.SubscriptionBillingStates.Add(Estado(1, "D", BillingStatePurpose.Membership, _prefijo + "-s2"));

        await db.SaveChangesAsync();
    }

    [SqlServerFact]
    public async Task No_puede_haber_dos_planes_de_cuota()
    {
        await using var db = SqlServerTestDb.Db();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Name = _prefijo + "-plan", CycleType = RecurringCycleType.AnnualFromLastBilling, RetryCadenceDays = "1",
            OnAllRetriesFail = RecurringFailurePolicy.MarkExpired, IsActive = false, IsAnnualFeePlan = true,
            CreatedBy = "sql-test", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should()
            .Match<DbUpdateException>(e => NumeroSql(e) == 2601 || NumeroSql(e) == 2627);
    }
}
