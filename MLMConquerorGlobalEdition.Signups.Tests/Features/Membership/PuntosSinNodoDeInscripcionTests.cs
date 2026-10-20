using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// Plan Q (tercera revision del bloque 2, menor): un miembro sin nodo de inscripcion sube sus puntos por la linea de su
/// patrocinador; si el patrocinador tampoco tiene nodo, se sube por la cadena de patrocinadores hasta el primero que lo
/// tenga (antes la linea se quedaba sin nada). Igual que 05-realinear-puntos-por-membresia.sql.
/// </summary>
public class PuntosSinNodoDeInscripcionTests
{
    private static MemberProfile Perfil(string id, string? patrocinador) => new()
    {
        MemberId = id, SponsorMemberId = patrocinador, FirstName = "A", LastName = "B", MemberType = MemberType.Ambassador,
        EnrollDate = DateTime.Now, Country = "US", CreatedBy = "seed", LastUpdateDate = DateTime.Now
    };

    [Fact]
    public async Task Sin_nodo_propio_ni_del_patrocinador_sube_por_el_primer_ascendiente_con_nodo()
    {
        await using var db = InMemoryDbHelper.Create();
        db.Products.Add(new Product
        {
            Id = "prod-3", Name = "Elite", Description = "", ImageUrl = "", MembershipLevelId = 3, QualificationPoins = 6,
            MonthlyFee = 10m, SetupFee = 0m, IsActive = true, CreatedBy = "seed"
        });
        db.MemberProfiles.AddRange(Perfil("R", null), Perfil("A", "R"), Perfil("S", "A"), Perfil("M", "S"));
        db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = "R", HierarchyPath = "/R/", Node = HierarchyId.Parse("/1/"), Level = 0, CreatedBy = "seed" },
            new GenealogyEntity { MemberId = "A", ParentMemberId = "R", HierarchyPath = "/R/A/", Node = HierarchyId.Parse("/1/1/"), Level = 1, CreatedBy = "seed" });
        var sus = new MembershipSubscription
        {
            MemberId = "M", MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Pending, StartDate = DateTime.Now,
            CreatedBy = "seed"
        };
        db.MembershipSubscriptions.Add(sus);
        await db.SaveChangesAsync();

        sus.SubscriptionStatus = MembershipStatus.Active;
        await db.SaveChangesAsync();

        // Plan Q (Bloque 4, Q-R34): la suya (sin nodo) y UNA de linea sobre A, el primer ascendiente con nodo; el trabajo
        // de deltas la expande a A y a R.
        var encolado = await db.MemberStatisticDeltas.AsNoTracking()
            .Where(d => d.SourceMemberId == "M" && d.CreatedBy == MembershipPointsDeltas.Actor)
            .Select(d => new { d.MemberId, d.Scope, d.EnrollmentPointsDelta }).ToListAsync();
        encolado.Should().BeEquivalentTo(new[]
        {
            new { MemberId = "M", Scope = MemberStatisticDeltaScope.Member, EnrollmentPointsDelta = 6 },
            new { MemberId = "A", Scope = MemberStatisticDeltaScope.SponsorLine, EnrollmentPointsDelta = 6 }
        }, "S no tiene nodo: la linea es la de A, el primer ascendiente con nodo, A incluido");
    }
}
