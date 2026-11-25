using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetTokenCodesGrid;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// El grid de códigos de token de un miembro filtra, ordena y pagina en el servidor sobre TODOS sus códigos: antes
/// la página pedía 500 y filtraba solo esos.
/// </summary>
public class TokenCodesGridHandlerTests
{
    private static readonly DateTime Base = new(2026, 11, 1, 9, 0, 0);

    /// <summary>
    /// 150 códigos de AMB-1, uno por minuto: los pares de tipo 1 ("VIP Member"), los impares de tipo 2 ("Guest Pass")
    /// y usados los múltiplos de 3. Además un asiento sin código de AMB-1 y un código de otro miembro, que no cuentan.
    /// </summary>
    private static async Task<AppDbContext> SeedAsync(int count = 150)
    {
        var db = InMemoryDbHelper.Create();
        db.TokenTypes.AddRange(
            new TokenType { Id = 1, Name = "VIP Member", IsActive = true, CreatedBy = "seed", CreationDate = Base },
            new TokenType { Id = 2, Name = "Guest Pass", IsGuestPass = true, IsActive = true, CreatedBy = "seed", CreationDate = Base });
        for (var i = 0; i < count; i++)
        {
            db.TokenTransactions.Add(new TokenTransaction
            {
                MemberId        = "AMB-1",
                TokenTypeId     = i % 2 == 0 ? 1 : 2,
                TransactionType = TokenTransactionType.Purchased,
                Quantity        = 1,
                ReferenceId     = $"CODE{i:D4}",
                UsedAt          = i % 3 == 0 ? Base.AddDays(1) : null,
                UsedByMemberId  = i % 3 == 0 ? $"MEM-{i:D4}" : null,
                CreationDate    = Base.AddMinutes(i),
                CreatedBy       = "seed"
            });
        }
        db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId = "AMB-1", TokenTypeId = 1, TransactionType = TokenTransactionType.Distributed, Quantity = 2,
            ReferenceId = null, CreationDate = Base.AddDays(5), CreatedBy = "seed"
        });
        db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId = "AMB-2", TokenTypeId = 1, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = "OTHER01", CreationDate = Base.AddDays(5), CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        // AuditInterceptor pone CreationDate = ahora al insertar: las fechas se fijan en una segunda escritura.
        foreach (var t in db.TokenTransactions.Local)
            t.CreationDate = t.ReferenceId is { } code && code.StartsWith("CODE")
                ? Base.AddMinutes(int.Parse(code[4..]))
                : Base.AddDays(5);
        await db.SaveChangesAsync();
        return db;
    }

    private static GridDataRequest Grid(int pageSize = 50, params GridFilter[] filters) =>
        new() { Page = 1, PageSize = pageSize, Filters = filters.ToList() };

    [Fact]
    public async Task ColumnFilter_SpansAllTheMembersCodes()
    {
        await using var db = await SeedAsync();
        var handler = new GetTokenCodesGridHandler(db);

        // CODE0000 es el más antiguo: con el orden por defecto cae en la última página.
        var result = await handler.Handle(new GetTokenCodesGridQuery("AMB-1",
            Grid(50, new GridFilter { Field = "TokenCode", Operator = "equal", Value = "CODE0000" })), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        var code = result.Value.Items.Should().ContainSingle().Subject;
        code.TokenTypeName.Should().Be("VIP Member");
        code.IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task ColumnFilter_OnTokenTypeName_ResolvedInTheQuery()
    {
        await using var db = await SeedAsync();
        var handler = new GetTokenCodesGridHandler(db);

        var result = await handler.Handle(new GetTokenCodesGridQuery("AMB-1",
            Grid(50, new GridFilter { Field = "TokenTypeName", Operator = "equal", Value = "Guest Pass" })), default);

        result.Value!.TotalCount.Should().Be(75);
        result.Value.Items.Should().OnlyContain(c => c.IsGuestPass && c.TokenTypeName == "Guest Pass");
    }

    [Fact]
    public async Task NoSort_DefaultsToNewestFirst_AndExcludesLedgerRowsAndOtherMembers()
    {
        await using var db = await SeedAsync();
        var handler = new GetTokenCodesGridHandler(db);

        var result = await handler.Handle(new GetTokenCodesGridQuery("AMB-1", Grid()), default);

        result.Value!.TotalCount.Should().Be(150);
        result.Value.Items.First().TokenCode.Should().Be("CODE0149");
        result.Value.Items.Select(c => c.CreatedDate).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task PageLevelFilters_TokenTypeAndIsUsed()
    {
        await using var db = await SeedAsync();
        var handler = new GetTokenCodesGridHandler(db);

        // Tipo 1 = pares (75); de esos, disponibles los que no son múltiplo de 3 (pares no múltiplos de 6): 50.
        var available = await handler.Handle(new GetTokenCodesGridQuery("AMB-1", Grid(), TokenTypeId: 1, IsUsed: false), default);
        var used      = await handler.Handle(new GetTokenCodesGridQuery("AMB-1", Grid(), TokenTypeId: 1, IsUsed: true), default);

        available.Value!.TotalCount.Should().Be(50);
        available.Value.Items.Should().OnlyContain(c => !c.IsUsed && c.TokenTypeName == "VIP Member");
        used.Value!.TotalCount.Should().Be(25);
        used.Value.Items.Should().OnlyContain(c => c.IsUsed);
    }

    [Fact]
    public async Task PageSize_IsCappedAt100()
    {
        await using var db = await SeedAsync();
        var handler = new GetTokenCodesGridHandler(db);

        var result = await handler.Handle(new GetTokenCodesGridQuery("AMB-1", Grid(pageSize: 500)), default);

        result.Value!.PageSize.Should().Be(100);
        result.Value.Items.Should().HaveCount(100);
    }
}
