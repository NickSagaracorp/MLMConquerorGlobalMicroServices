using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateCarBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

/// <summary>
/// Plan M (revisión M6): el bono de coche suma el equipo por Node (nodo vigente de cada miembro), no
/// con HierarchyPath.Contains en memoria, y un miembro con dos nodos no rompe el cálculo.
/// </summary>
public class CalculateCarBonusHandlerTests
{
    private static readonly DateTime Ahora = new(2026, 3, 23, 12, 0, 0);

    private static CalculateCarBonusHandler Handler(Repository.Context.AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("system");
        return new CalculateCarBonusHandler(db, reloj.Object, usuario.Object);
    }

    private static CommissionType Tipo(int equipo) => new()
    {
        Id = 85, CommissionCategoryId = 4, Name = "Car Bonus", IsActive = true,
        PersonalPoints = 6, TeamPoints = equipo, Amount = 500m, CreatedBy = "seed", CreationDate = Ahora
    };

    private static void Elite(Repository.Context.AppDbContext db, string id) => db.MembershipSubscriptions.Add(new MembershipSubscription
    {
        MemberId = id, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
        StartDate = Ahora.AddMonths(-2), CreatedBy = "seed", CreationDate = Ahora.AddMonths(-2)
    });

    private static GenealogyEntity Nodo(Repository.Context.AppDbContext db, string id, string? node, DateTime? cuando = null)
    {
        var g = new GenealogyEntity
        {
            MemberId = id, HierarchyPath = "/" + id + "/", Level = 1,
            Node = node is null ? null : HierarchyId.Parse(node), CreatedBy = "seed"
        };
        db.GenealogyTree.Add(g);
        return g;
    }

    [Fact]
    public async Task El_equipo_se_suma_por_Node_y_quien_tiene_dos_nodos_cuenta_por_el_vigente()
    {
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(Tipo(equipo: 12));
        foreach (var id in new[] { "A", "B", "C", "D", "E" }) Elite(db, id);
        Nodo(db, "A", "/1/");
        Nodo(db, "B", "/1/1/");
        Nodo(db, "C", "/1/1/1/");
        Nodo(db, "D", "/2/");
        var viejo = Nodo(db, "E", "/1/2/");   // E estuvo bajo A...
        Nodo(db, "E", "/2/1/");               // ...y hoy cuelga de D
        await db.SaveChangesAsync();
        viejo.CreationDate = Ahora.AddYears(-1);
        await db.SaveChangesAsync();

        var r = await Handler(db).Handle(new CalculateCarBonusCommand(null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var ganadores = await db.CommissionEarnings.Select(e => e.BeneficiaryMemberId).ToListAsync();
        ganadores.Should().Equal(new[] { "A" }, "A suma B y C (12); E ya no está en su equipo; D solo suma a E (6)");
    }

    [Fact]
    public async Task Un_embajador_sin_Node_se_omite_y_se_dice_por_que()
    {
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(Tipo(equipo: 0));
        Elite(db, "SIN");
        Nodo(db, "SIN", null);
        await db.SaveChangesAsync();

        var r = await Handler(db).Handle(new CalculateCarBonusCommand(null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value!.RecordsCreated.Should().Be(0);
        r.Value.SkippedReasons.Should().Contain(s => s.Contains("SIN") && s.Contains("Node"));
    }
}
