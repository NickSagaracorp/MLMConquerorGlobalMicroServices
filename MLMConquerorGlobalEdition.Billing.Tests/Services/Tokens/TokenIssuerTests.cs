using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>Una instancia canjeable por token, con código único, dueño original y vínculo a la compra.</summary>
public class TokenIssuerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    [Fact]
    public async Task Emite_una_instancia_canjeable_por_token_y_crea_el_saldo()
    {
        var db = TestDbContextFactory.Create();

        var codes = await new TokenIssuer(db).IssueAsync(new TokenIssue("AMB-1", 13, 3, "purchase-1", "TP1015-1", "user-1", Now));
        await db.SaveChangesAsync();

        codes.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        var rows = db.TokenTransactions.ToList();
        // Sin comparar CreationDate: AuditInterceptor la pisa con la hora de la máquina al guardar,
        // que es la misma hora de servidor con la que el cupo cuenta el mes.
        rows.Should().HaveCount(3).And.OnlyContain(t =>
            t.MemberId == "AMB-1" && t.OriginalOwnerMemberId == "AMB-1" && t.TokenTypeId == 13 &&
            t.TransactionType == TokenTransactionType.Purchased && t.Quantity == 1 &&
            t.Status == TokenInstanceStatus.Issued && t.TokenPurchaseId == "purchase-1");
        rows.Select(t => t.ReferenceId).Should().BeEquivalentTo(codes);
        db.TokenBalances.Single().Balance.Should().Be(3);
    }

    [Fact]
    public async Task Suma_al_saldo_que_ya_existe()
    {
        var db = TestDbContextFactory.Create();
        db.TokenBalances.Add(new TokenBalance { MemberId = "AMB-1", TokenTypeId = 13, Balance = 4, CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        await new TokenIssuer(db).IssueAsync(new TokenIssue("AMB-1", 13, 2, "p", null, "user-1", Now));
        await db.SaveChangesAsync();

        db.TokenBalances.Single().Balance.Should().Be(6);
    }

    [Fact]
    public async Task No_emite_cero()
    {
        var act = () => new TokenIssuer(TestDbContextFactory.Create())
            .IssueAsync(new TokenIssue("AMB-1", 13, 0, "p", null, "user-1", Now));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
