using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Los hechos del miembro para la cuota (Plan K): el perfil y si su membresía VIGENTE está en
/// HoldByBilling (decisión de Nick, 2026-10-17). La vigente es la suscripción no borrada más reciente
/// por StartDate y luego CreationDate, la misma regla que QualificationService.
/// </summary>
public class AnnualFeeMemberQueriesTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Hoy = new(2027, 6, 10);

    private static async Task<AppDbContext> ConPerfilAsync()
    {
        var db = TestDbContextFactory.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = Miembro, Email = "amb-1@prueba.test", FirstName = "N", LastName = "A",
            Country = "US", MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
            EnrollDate = new DateTime(2026, 6, 5), KeepQualified = true, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static void Suscripcion(AppDbContext db, MembershipStatus estado, DateTime inicio, bool borrada = false)
        => db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = estado, StartDate = inicio,
            IsDeleted = borrada, CreatedBy = "prueba"
        });

    [Fact]
    public async Task Sin_perfil_es_como_no_tenerlo()
    {
        await using var db = TestDbContextFactory.Create();
        (await AnnualFeeMemberQueries.FactsAsync(db, "NADIE")).Exists.Should().BeFalse();
    }

    [Fact]
    public async Task Lee_el_perfil_y_sin_suscripcion_no_esta_en_espera()
    {
        await using var db = await ConPerfilAsync();

        var f = await AnnualFeeMemberQueries.FactsAsync(db, Miembro);

        f.Should().Be(new AnnualFeeMemberFacts(true, MemberType.Ambassador, MemberAccountStatus.Active,
            new DateTime(2026, 6, 5), true, false));
    }

    [Fact]
    public async Task La_membresia_vigente_en_HoldByBilling_esta_en_espera()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddYears(-1));
        Suscripcion(db, MembershipStatus.HoldByBilling, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro)).MembershipOnHold.Should().BeTrue();
    }

    [Fact]
    public async Task Una_espera_antigua_con_una_membresia_activa_mas_reciente_no_cuenta()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.HoldByBilling, Hoy.AddYears(-1));
        Suscripcion(db, MembershipStatus.Active, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro)).MembershipOnHold.Should().BeFalse();
    }

    [Fact]
    public async Task Con_el_mismo_inicio_decide_la_creada_despues()
    {
        // AuditInterceptor pone CreationDate al guardar: se guardan por separado, la espera después.
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();
        await Task.Delay(20);
        Suscripcion(db, MembershipStatus.HoldByBilling, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro)).MembershipOnHold.Should().BeTrue();
    }

    [Fact]
    public async Task Una_suscripcion_borrada_no_cuenta()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddYears(-1));
        Suscripcion(db, MembershipStatus.HoldByBilling, Hoy.AddMonths(-2), borrada: true);
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro)).MembershipOnHold.Should().BeFalse();
    }

    [Theory]
    [InlineData(MembershipStatus.OnHold)]
    [InlineData(MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Cancelled)]
    public async Task Solo_HoldByBilling_excluye(MembershipStatus estado)
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, estado, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro)).MembershipOnHold.Should().BeFalse();
    }
}
