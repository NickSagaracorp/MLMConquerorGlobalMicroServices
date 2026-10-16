using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// Los códigos de token son al portador: quien lo lee lo canjea. Soporte ve la ficha del miembro pero
/// no necesita el código entero; solo SuperAdmin y Admin lo ven completo. También se tapan los códigos
/// que lleva la nota de una distribución o una transferencia.
/// </summary>
public class CodigosEnmascaradosParaSoporteTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0);

    private static async Task<AppDbContext> Db()
    {
        var db = InMemoryDbHelper.Create();
        db.TokenTypes.Add(new TokenType { Id = 13, Name = "VIP Member", IsActive = true, CreatedBy = "seed", CreationDate = Now });
        db.TokenTransactions.AddRange(
            new TokenTransaction
            {
                MemberId = "AMB-1", TokenTypeId = 13, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
                ReferenceId = "K7Q4X29", Status = TokenInstanceStatus.Issued, CreatedBy = "seed", CreationDate = Now
            },
            new TokenTransaction
            {
                MemberId = "AMB-1", TokenTypeId = 13, TransactionType = TokenTransactionType.Distributed, Quantity = 2,
                ReferenceId = null, Status = TokenInstanceStatus.Distributed, CreatedBy = "seed", CreationDate = Now.AddMinutes(1),
                Notes = "2 token(s) distributed to AMB-100002: 4AB12C3, Z9Q8X77"
            });
        await db.SaveChangesAsync();
        return db;
    }

    private static AdminMemberTokensController Controller(AppDbContext db, string role)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        return new AdminMemberTokensController(db, clock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "test"))
                }
            }
        };
    }

    private static async Task<List<MemberTokenQueries.HistoryRow>> Historial(AppDbContext db, string role)
    {
        var r = await Controller(db, role).GetHistory("AMB-1", 1, 20);
        return ((ApiResponse<PagedResult<MemberTokenQueries.HistoryRow>>)((OkObjectResult)r).Value!).Data!.Items.ToList();
    }

    [Theory]
    [InlineData("SupportManager")]
    [InlineData("SupportLevel1")]
    [InlineData("SupportLevel2")]
    [InlineData("SupportLevel3")]
    public async Task Soporte_ve_el_codigo_enmascarado(string role)
    {
        await using var db = await Db();

        var rows = await Historial(db, role);

        rows.Single(r => r.TransactionType == "Purchased").TokenCode.Should().Be("K7Q****");
        var nota = rows.Single(r => r.TransactionType == "Distributed").Notes;
        nota.Should().Be("2 token(s) distributed to AMB-100002: 4AB****, Z9Q****");
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Admin")]
    public async Task SuperAdmin_y_Admin_ven_el_codigo_entero(string role)
    {
        await using var db = await Db();

        var rows = await Historial(db, role);

        rows.Single(r => r.TransactionType == "Purchased").TokenCode.Should().Be("K7Q4X29");
        rows.Single(r => r.TransactionType == "Distributed").Notes.Should().Contain("4AB12C3, Z9Q8X77");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("AB", "****")]
    [InlineData("K7Q4X29", "K7Q****")]
    public void Mask_deja_tres_caracteres(string? code, string? expected)
        => TokenCodeMask.Mask(code).Should().Be(expected);

    [Fact]
    public void En_un_texto_solo_tapa_lo_que_tiene_forma_de_codigo()
        => TokenCodeMask.MaskCodesIn("Token K7Q4X29 → AMB-100002. Pedido TP-2026101512 1234567 ABCDEFG")
            .Should().Be("Token K7Q**** → AMB-100002. Pedido TP-2026101512 1234567 ABCDEFG");
}
