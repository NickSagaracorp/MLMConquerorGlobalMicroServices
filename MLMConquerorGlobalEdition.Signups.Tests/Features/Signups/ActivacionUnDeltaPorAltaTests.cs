using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>Plan Q (Bloque 4): la activacion de un alta encola UNA fila de estadisticas para toda la linea.</summary>
public class ActivacionUnDeltaPorAltaTests
{
    [Fact]
    public async Task La_activacion_deja_un_solo_delta_de_linea_con_el_patrocinador_a_12_niveles()
    {
        await using var db = InMemoryDbHelper.Create();
        var linea = Enumerable.Range(1, 12).Select(i => $"AMB-{i:D6}").ToArray();   // linea[^1] es el patrocinador
        db.GenealogyTree.Add(new GenealogyEntity
        {
            MemberId = linea[^1], ParentMemberId = linea[^2], HierarchyPath = "/" + string.Join("/", linea) + "/",
            Node = HierarchyId.Parse("/" + string.Join("/", Enumerable.Repeat(1, 12)) + "/"), Level = 12, CreatedBy = "seed"
        });
        var nuevo = new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = "AMB-NUEVO", SponsorMemberId = linea[^1], FirstName = "Nuevo", LastName = "Alta",
            Email = "nuevo@prueba.test", Country = "US", MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Pending, CreatedBy = "seed"
        };
        var pedido = new Orders { MemberId = "AMB-NUEVO", Status = OrderStatus.Pending, CreatedBy = "seed" };
        var suscripcion = new MembershipSubscription
        {
            MemberId = "AMB-NUEVO", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Pending, CreatedBy = "seed"
        };
        db.AddRange(nuevo, pedido, suscripcion);
        await db.SaveChangesAsync();
        var servicio = new SignupActivationService(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
            Mock.Of<IRecurringBillingEnrollmentService>());

        await servicio.ActivateAsync(pedido, nuevo, suscripcion, totalQualificationPoints: 6,
            new DateTime(2026, 10, 19, 12, 0, 0), "nuevo@prueba.test", CancellationToken.None);
        await db.SaveChangesAsync();

        // DQ16: los puntos del pedido ya no van en la fila del alta (los encola MembershipPointsDeltas al pasar a Active).
        var delta = (await db.MemberStatisticDeltas.Where(d => d.Scope == MemberStatisticDeltaScope.SponsorLine).ToListAsync())
            .Should().ContainSingle().Subject;
        (delta.MemberId, delta.Scope, delta.EnrollmentPointsDelta, delta.EnrollmentTeamSizeDelta,
         delta.QualifiedSponsoredMembersDelta, delta.SourceMemberId, delta.IsApplied)
            .Should().Be((linea[^1], MemberStatisticDeltaScope.SponsorLine, 0, 1, 1, "AMB-NUEVO", false));
        // Tarea 19b (DQ13): y la de los calificados de inscripcion del activado, una sola para todos sus ascendientes.
        (await db.MemberStatisticDeltas.Where(d => d.Scope == MemberStatisticDeltaScope.AncestorsOf)
                .Select(d => new { d.MemberId, d.EnrollmentQualifiedTeamMembersDelta }).ToListAsync())
            .Should().ContainSingle().Which.Should().Be(new { MemberId = "AMB-NUEVO", EnrollmentQualifiedTeamMembersDelta = 1 });
    }
}
