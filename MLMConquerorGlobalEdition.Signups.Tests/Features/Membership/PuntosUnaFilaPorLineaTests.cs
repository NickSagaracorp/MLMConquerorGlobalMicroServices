using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// Plan Q (Bloque 4, Q-R34): un cambio de estado encola UNA fila de estadisticas para la linea de patrocinio (antes, una por
/// ascendiente) y la evaluacion de rango llega solo a la linea cercana: el propio y sus GenealogyUpline.EnrollmentLevels ascendientes.
/// </summary>
public class PuntosUnaFilaPorLineaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => Ahora;
        public DateTime UtcNow => Ahora;
    }

    [Fact]
    public async Task Activar_a_12_niveles_deja_una_fila_y_tras_el_trabajo_llega_a_toda_la_linea()
    {
        await using var db = InMemoryDbHelper.Create();
        db.Products.Add(new Product
        {
            Id = "prod-3", Name = "Elite", Description = "", ImageUrl = "", MembershipLevelId = 3, QualificationPoins = 6,
            MonthlyFee = 10m, SetupFee = 0m, IsActive = true, CreatedBy = "seed"
        });
        // L00..L11 son los 12 ascendientes; M cuelga de L11.
        var linea = Enumerable.Range(0, 12).Select(i => $"AMB-L{i:D2}").Append("AMB-M").ToArray();
        for (var i = 0; i < linea.Length; i++)
            db.GenealogyTree.Add(new GenealogyEntity
            {
                MemberId = linea[i], ParentMemberId = i == 0 ? null : linea[i - 1],
                HierarchyPath = "/" + string.Join("/", linea[..(i + 1)]) + "/", Level = i + 1,
                Node = HierarchyId.Parse("/" + string.Join("/", Enumerable.Repeat(1, i + 1)) + "/"),
                CreatedBy = "seed", CreationDate = Ahora
            });
        var sus = new MembershipSubscription
        {
            MemberId = "AMB-M", MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Pending, StartDate = Ahora,
            CreatedBy = "seed"
        };
        db.MembershipSubscriptions.Add(sus);
        await db.SaveChangesAsync();

        sus.SubscriptionStatus = MembershipStatus.Active;
        await db.SaveChangesAsync();

        var fila = (await db.MemberStatisticDeltas.AsNoTracking()
                .Where(d => d.CreatedBy == MembershipPointsDeltas.Actor).ToListAsync())
            .Should().ContainSingle("una fila por cambio, no una por ascendiente").Subject;
        (fila.MemberId, fila.Scope, fila.EnrollmentPointsDelta, fila.EnrollmentTeamSizeDelta, fila.QualifiedSponsoredMembersDelta)
            .Should().Be(("AMB-M", MemberStatisticDeltaScope.SponsorLine, 6, 0, 0));

        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance)
            .ExecuteAsync(CancellationToken.None);

        var stats = await db.MemberStatistics.AsNoTracking().ToDictionaryAsync(s => s.MemberId);
        linea.Should().OnlyContain(id => stats[id].EnrollmentPoints == 6, "el propio y sus 12 ascendientes");
        (await db.RankEvaluationQueue.Where(q => !q.IsProcessed).Select(q => q.EvaluateMemberId).ToListAsync())
            .Should().BeEquivalentTo(linea[^11..], "el propio y sus 10 ascendientes mas cercanos (revision del bloque 4, como en un alta); el resto, el barrido nocturno");
    }
}
