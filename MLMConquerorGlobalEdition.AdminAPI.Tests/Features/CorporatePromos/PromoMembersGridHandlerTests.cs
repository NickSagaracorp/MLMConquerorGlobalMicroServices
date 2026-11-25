using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoMembersGrid;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CorporatePromos;

/// <summary>
/// El grid de miembros de una promo filtra, ordena y pagina en el servidor sobre TODOS los miembros de la promo:
/// antes la página pedía 500 y filtraba solo esos.
/// </summary>
public class PromoMembersGridHandlerTests
{
    private static readonly DateTime Inicio = new(2026, 10, 1);
    private static readonly DateTime Fin    = new(2026, 10, 31, 23, 59, 59);

    /// <summary>
    /// Promo de octubre con 150 miembros dados de alta en ella, uno por hora (AMB-0000 el primero). Los múltiplos de 5
    /// tienen la suscripción cancelada. AMB-0000 además renovó a "Elite" después. Fuera de la promo: AMB-OUT, que se dio
    /// de alta en septiembre.
    /// </summary>
    private static async Task<AppDbContext> SeedAsync(int count = 150)
    {
        var db = InMemoryDbHelper.Create();
        db.CorporatePromos.Add(new CorporatePromo
        {
            Id = "PROMO-1", Title = "October", StartDate = Inicio, EndDate = Fin, IsActive = true, CreatedBy = "seed"
        });
        db.MembershipLevels.AddRange(
            new MembershipLevel { Id = 1, Name = "Ambassador", SortOrder = 1, IsActive = true, CreatedBy = "seed", CreationDate = Inicio },
            new MembershipLevel { Id = 2, Name = "Elite", SortOrder = 2, IsActive = true, CreatedBy = "seed", CreationDate = Inicio });

        for (var i = 0; i < count; i++)
        {
            var id = $"AMB-{i:D4}";
            db.MemberProfiles.Add(Profile(id, $"First{i}", $"Last{i}", $"m{i}@test.com"));
            db.MembershipSubscriptions.Add(new MembershipSubscription
            {
                MemberId = id, MembershipLevelId = 1,
                SubscriptionStatus = i % 5 == 0 ? MembershipStatus.Cancelled : MembershipStatus.Active,
                ChangeReason = SubscriptionChangeReason.New,
                CreationDate = Inicio.AddHours(i), CreatedBy = "seed"
            });
        }

        // La última suscripción manda en nivel y estado; la fecha de alta sigue siendo la primera dentro de la promo.
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = "AMB-0000", MembershipLevelId = 2, SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New, CreationDate = Fin.AddDays(10), CreatedBy = "seed"
        });

        db.MemberProfiles.Add(Profile("AMB-OUT", "Out", "Side", "out@test.com"));
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = "AMB-OUT", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New, CreationDate = Inicio.AddDays(-10), CreatedBy = "seed"
        });

        // AuditInterceptor pone CreationDate = ahora al insertar: se guardan las fechas deseadas y se fijan en una
        // segunda escritura (al modificar no la toca).
        var fechas = db.MembershipSubscriptions.Local.ToDictionary(s => s, s => s.CreationDate);
        await db.SaveChangesAsync();
        foreach (var (s, fecha) in fechas) s.CreationDate = fecha;
        await db.SaveChangesAsync();
        return db;
    }

    private static MemberProfile Profile(string id, string first, string last, string email) => new()
    {
        MemberId = id, FirstName = first, LastName = last, Email = email,
        MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
        EnrollDate = Inicio, CreationDate = Inicio, CreatedBy = "seed", LastUpdateDate = Inicio
    };

    private static GridDataRequest Grid(int pageSize = 50, params GridFilter[] filters) =>
        new() { Page = 1, PageSize = pageSize, Filters = filters.ToList() };

    [Fact]
    public async Task ColumnFilter_SpansAllPromoMembers()
    {
        await using var db = await SeedAsync();
        var handler = new GetPromoMembersGridHandler(db);

        // AMB-0000 es el primero en darse de alta: con el orden por defecto cae en la última página.
        var result = await handler.Handle(new GetPromoMembersGridQuery("PROMO-1",
            Grid(50, new GridFilter { Field = "Email", Operator = "equal", Value = "m0@test.com" })), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        var m = result.Value.Items.Should().ContainSingle().Subject;
        m.MemberId.Should().Be("AMB-0000");
        m.FullName.Should().Be("First0 Last0");
        m.MembershipLevel.Should().Be("Elite");
        m.SubscriptionStatus.Should().Be("Active");
        m.SignupDate.Should().Be(Inicio);
    }

    [Fact]
    public async Task ColumnFilter_OnSubscriptionStatus_ResolvedInTheQuery()
    {
        await using var db = await SeedAsync();
        var handler = new GetPromoMembersGridHandler(db);

        var result = await handler.Handle(new GetPromoMembersGridQuery("PROMO-1",
            Grid(50, new GridFilter { Field = "SubscriptionStatus", Operator = "equal", Value = "Cancelled" })), default);

        // Múltiplos de 5 entre 0 y 149 son 30, menos AMB-0000, que renovó y está activo.
        result.Value!.TotalCount.Should().Be(29);
    }

    [Fact]
    public async Task NoSort_DefaultsToNewestSignupFirst_AndOnlyPromoWindow()
    {
        await using var db = await SeedAsync();
        var handler = new GetPromoMembersGridHandler(db);

        var result = await handler.Handle(new GetPromoMembersGridQuery("PROMO-1", Grid()), default);

        result.Value!.TotalCount.Should().Be(150);
        result.Value.Items.First().MemberId.Should().Be("AMB-0149");
        result.Value.Items.Select(m => m.SignupDate).Should().BeInDescendingOrder();
        result.Value.Items.Should().NotContain(m => m.MemberId == "AMB-OUT");
    }

    [Fact]
    public async Task UnknownPromo_FailsWithPromoNotFound()
    {
        await using var db = await SeedAsync(3);
        var handler = new GetPromoMembersGridHandler(db);

        var result = await handler.Handle(new GetPromoMembersGridQuery("NOPE", Grid()), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMO_NOT_FOUND");
    }

    [Fact]
    public async Task PageSize_IsCappedAt100()
    {
        await using var db = await SeedAsync();
        var handler = new GetPromoMembersGridHandler(db);

        var result = await handler.Handle(new GetPromoMembersGridQuery("PROMO-1", Grid(pageSize: 500)), default);

        result.Value!.PageSize.Should().Be(100);
        result.Value.Items.Should().HaveCount(100);
    }
}
