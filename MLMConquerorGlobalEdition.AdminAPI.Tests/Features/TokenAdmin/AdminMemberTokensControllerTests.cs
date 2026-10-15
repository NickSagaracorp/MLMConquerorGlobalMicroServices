using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// La pestaña de tokens de la ficha del miembro en AdminWeb llama a
/// <c>api/v1/admin/members/{id}/tokens</c> y <c>…/tokens/summary</c>, que no existían. Estas rutas
/// son de solo lectura, con la misma forma que las del BizCenter, y del miembro de la ruta.
/// </summary>
public class AdminMemberTokensControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0);

    private static async Task<AppDbContext> Db()
    {
        var db = InMemoryDbHelper.Create();
        db.TokenTypes.Add(new TokenType { Id = 13, Name = "VIP Member", IsActive = true, CreatedBy = "seed", CreationDate = Now });
        TokenTransaction Codigo(string member, string code, TokenInstanceStatus status, DateTime? expires = null, int minutos = 0) => new()
        {
            MemberId = member, TokenTypeId = 13, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = code, Status = status, ExpiresAt = expires, CreatedBy = "seed", CreationDate = Now.AddMinutes(minutos)
        };
        db.TokenTransactions.AddRange(
            Codigo("AMB-1", "AAA0001", TokenInstanceStatus.Issued, minutos: 1),
            Codigo("AMB-1", "AAA0002", TokenInstanceStatus.Used, minutos: 2),
            Codigo("AMB-1", "AAA0003", TokenInstanceStatus.Issued, expires: Now.AddDays(-1), minutos: 3),
            Codigo("AMB-2", "BBB0001", TokenInstanceStatus.Issued));
        db.TokenBalances.AddRange(
            new TokenBalance { MemberId = "AMB-1", TokenTypeId = 13, Balance = 2, CreatedBy = "seed", CreationDate = Now },
            new TokenBalance { MemberId = "AMB-2", TokenTypeId = 13, Balance = 9, CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();
        return db;
    }

    private static AdminMemberTokensController Controller(AppDbContext db)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        return new AdminMemberTokensController(db, clock.Object);
    }

    private static T DataOf<T>(IActionResult r) =>
        ((ApiResponse<T>)r.Should().BeOfType<OkObjectResult>().Subject.Value!).Data!;

    [Fact]
    public async Task El_resumen_es_el_del_miembro_de_la_ruta()
    {
        await using var db = await Db();

        var s = DataOf<TokenSummaryDto>(await Controller(db).GetSummary("AMB-1"));

        s.TotalTokens.Should().Be(3);
        s.TotalUsed.Should().Be(1);
        s.TotalAvailable.Should().Be(1, "el vencido no cuenta como disponible");
    }

    [Fact]
    public async Task El_historial_va_paginado_y_de_lo_mas_reciente_a_lo_mas_antiguo()
    {
        await using var db = await Db();

        var page = DataOf<PagedResult<MemberTokenQueries.HistoryRow>>(await Controller(db).GetHistory("AMB-1", page: 1, pageSize: 2));

        page.TotalCount.Should().Be(3);
        page.Items.Should().HaveCount(2).And.BeInDescendingOrder(i => i.OccurredAt);
        page.Items.Select(i => i.TokenCode).Should().OnlyContain(c => c!.StartsWith("AAA"), "solo los del miembro de la ruta");
        page.Items.First().TokenTypeName.Should().Be("VIP Member");
    }

    [Fact]
    public async Task Los_saldos_son_los_del_miembro_de_la_ruta()
    {
        await using var db = await Db();

        var saldos = DataOf<List<MemberTokenQueries.BalanceRow>>(await Controller(db).GetBalances("AMB-1"));

        saldos.Should().ContainSingle().Which.Balance.Should().Be(2);
    }

    [Fact]
    public void Es_de_solo_lectura_y_para_los_roles_que_ven_la_ficha()
    {
        typeof(AdminMemberTokensController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be("SuperAdmin,Admin,SupportManager,SupportLevel1,SupportLevel2,SupportLevel3");
        typeof(AdminMemberTokensController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/v1/admin/members/{memberId}/tokens");

        var verbos = typeof(AdminMemberTokensController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Distinct();
        verbos.Should().Equal("GET");
    }
}
