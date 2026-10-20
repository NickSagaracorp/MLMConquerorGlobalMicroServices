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

    private static void Suscripcion(AppDbContext db, MembershipStatus estado, DateTime inicio, bool borrada = false,
        bool renueva = true, DateTime? cancelacion = null)
        => db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = estado, StartDate = inicio,
            IsDeleted = borrada, IsAutoRenew = renueva, CancellationDate = cancelacion, CreatedBy = "prueba"
        });

    [Fact]
    public async Task Sin_perfil_es_como_no_tenerlo()
    {
        await using var db = TestDbContextFactory.Create();
        (await AnnualFeeMemberQueries.FactsAsync(db, "NADIE", Hoy)).Exists.Should().BeFalse();
    }

    [Fact]
    public async Task Lee_el_perfil_y_sin_suscripcion_no_esta_en_espera()
    {
        await using var db = await ConPerfilAsync();

        var f = await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy);

        f.Should().Be(new AnnualFeeMemberFacts(true, MemberType.Ambassador, MemberAccountStatus.Active,
            new DateTime(2026, 6, 5), true, false, false));
    }

    [Fact]
    public async Task La_membresia_vigente_en_HoldByBilling_esta_en_espera()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddYears(-1));
        Suscripcion(db, MembershipStatus.HoldByBilling, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy)).MembershipOnHold.Should().BeTrue();
    }

    [Fact]
    public async Task Una_espera_antigua_con_una_membresia_activa_mas_reciente_no_cuenta()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.HoldByBilling, Hoy.AddYears(-1));
        Suscripcion(db, MembershipStatus.Active, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy)).MembershipOnHold.Should().BeFalse();
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

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy)).MembershipOnHold.Should().BeTrue();
    }

    [Fact]
    public async Task Una_suscripcion_borrada_no_cuenta()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddYears(-1));
        Suscripcion(db, MembershipStatus.HoldByBilling, Hoy.AddMonths(-2), borrada: true);
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy)).MembershipOnHold.Should().BeFalse();
    }

    // Revision de Q3 (I4): OnHold tambien excluye (La_membresia_vigente_retenida_OnHold_esta_en_espera).
    [Theory]
    [InlineData(MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Cancelled)]
    public async Task Solo_HoldByBilling_y_OnHold_excluyen(MembershipStatus estado)
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, estado, Hoy.AddMonths(-2));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy)).MembershipOnHold.Should().BeFalse();
    }

    // ── Membresía cancelada (decisión de Nick, M-8) ──────────────────────────────

    [Fact]
    public async Task La_membresia_vigente_cancelada_cuenta_como_cancelada()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Cancelled, Hoy.AddMonths(-2), renueva: false);
        await db.SaveChangesAsync();

        var f = await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy);
        f.MembershipCancelled.Should().BeTrue();
        f.MembershipOnHold.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, true)]  // la cancelación programada ya se cumplió
    [InlineData(0, true)]   // se cumple hoy
    [InlineData(5, false)]  // aún no
    public async Task La_cancelacion_programada_cuenta_cuando_se_cumple(int dias, bool esperado)
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddMonths(-2), renueva: false, cancelacion: Hoy.AddDays(dias));
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy)).MembershipCancelled.Should().Be(esperado);
    }

    [Fact]
    public async Task Sin_renovacion_automatica_pero_sin_fecha_no_esta_cancelada()
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddMonths(-2), renueva: false);
        await db.SaveChangesAsync();

        (await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy)).MembershipCancelled.Should().BeFalse();
    }

    /// <summary>
    /// Revision de Q3 (I4): la membresia vigente retenida por el admin (OnHold, al suspender o inactivar la cuenta) cuenta
    /// como en espera igual que HoldByBilling: la cuota anual se pospone y "Pagar ahora" no se ofrece.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task La_membresia_vigente_retenida_OnHold_esta_en_espera(bool conMarcaDelAdmin)
    {
        await using var db = await ConPerfilAsync();
        Suscripcion(db, MembershipStatus.Active, Hoy.AddYears(-1));
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.OnHold, StartDate = Hoy.AddMonths(-2),
            HeldByAccountStatus = conMarcaDelAdmin ? MemberAccountStatus.Inactive : null, IsAutoRenew = true, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var hechos = await AnnualFeeMemberQueries.FactsAsync(db, Miembro, Hoy);

        hechos.MembershipOnHold.Should().BeTrue();
        AnnualFeeEligibility.ForAutomaticCharge(hechos with { Status = MemberAccountStatus.Inactive, KeepQualified = false },
                Hoy, null, Hoy, force: true)
            .Should().Be(new AnnualFeeDecision(AnnualFeeDecisionKind.Postpone, AnnualFeeEligibility.ReasonMembershipOnHold));
    }
}
