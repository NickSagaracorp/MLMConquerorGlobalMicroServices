using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.DowngradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// Plan Q (tercera revision del bloque 2, C-a): una subida o bajada caduca la suscripcion vieja y PARA su estado de cobro
/// en el mismo guardado. Antes el estado seguia vivo: el motor cobraba el nivel viejo en el siguiente ciclo, reactivaba la
/// vieja caducada (los puntos contaban dos veces) y el barrido creaba otro estado para la nueva (dos cobros por miembro).
/// </summary>
public class CambioDeNivelParaElCobroTests
{
    private static readonly DateTime Ahora = new(2026, 10, 19, 10, 0, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync()
    {
        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = Miembro, FirstName = "A", LastName = "B", MemberType = MemberType.Ambassador,
            EnrollDate = Ahora.AddDays(-30), Country = "US", CreatedBy = "seed", LastUpdateDate = Ahora
        });
        db.MembershipLevels.AddRange(
            new MembershipLevel { Id = 1, Name = "VIP", SortOrder = 10, IsActive = true, IsAutoRenew = true, CreatedBy = "seed" },
            new MembershipLevel { Id = 2, Name = "Elite", SortOrder = 20, IsActive = true, IsAutoRenew = true, CreatedBy = "seed" },
            new MembershipLevel { Id = 3, Name = "Turbo", SortOrder = 30, IsActive = true, IsAutoRenew = true, CreatedBy = "seed" });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-vieja", MemberId = Miembro, MembershipLevelId = 2, SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New, StartDate = Ahora.AddDays(-30), IsAutoRenew = true,
            CreatedBy = "seed", CreationDate = Ahora.AddDays(-30)
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-viejo", MembershipSubscriptionId = "sub-vieja", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-30), NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static IDateTimeProvider Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Ahora);
        return m.Object;
    }

    private static async Task ComprobarParadoAsync(AppDbContext db)
    {
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync(s => s.Id == "estado-viejo");
        estado.Status.Should().Be(RecurringBillingStatus.Stopped, "la suscripcion vieja ya no se renueva");
        estado.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipReplaced);
    }

    [Fact]
    public async Task La_subida_para_el_estado_de_cobro_de_la_suscripcion_vieja()
    {
        await using var db = await SembrarAsync();

        var r = await new UpgradeMembershipHandler(db, Reloj(), SubidaDeNivel.Encendida())
            .Handle(new UpgradeMembershipCommand(Miembro, 3, null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        await ComprobarParadoAsync(db);
    }

    [Fact]
    public async Task La_bajada_para_el_estado_de_cobro_de_la_suscripcion_vieja()
    {
        await using var db = await SembrarAsync();

        var r = await new DowngradeMembershipHandler(db, Reloj())
            .Handle(new DowngradeMembershipCommand(Miembro, 1, null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        await ComprobarParadoAsync(db);
    }
}
