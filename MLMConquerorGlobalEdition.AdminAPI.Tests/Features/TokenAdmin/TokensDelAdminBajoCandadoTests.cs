using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GrantTokens;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.UpdateTokenBalance;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// Conceder tokens y ajustar el saldo tocan el TokenBalance del miembro: van bajo su candado de
/// fondos, el mismo de comprar, distribuir y transferir (revisión de la Tarea 14, I2).
/// </summary>
public class TokensDelAdminBajoCandadoTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static ICurrentUserService Usuario()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m.Object;
    }

    private static IDateTimeProvider Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Now);
        return m.Object;
    }

    private static async Task<MLMConquerorGlobalEdition.Repository.Context.AppDbContext> ConTipoAsync()
    {
        var db = InMemoryDbHelper.Create();
        db.TokenTypes.Add(new TokenType { Id = 1, Name = "VIP", IsActive = true, CreatedBy = "seed" });
        await db.SaveChangesAsync();
        return db;
    }

    private static GrantTokensCommand Conceder(int qty = 2) =>
        new(new AdminGrantTokenRequest { MemberId = "AMB-001", TokenTypeId = 1, Quantity = qty });

    [Fact]
    public async Task Conceder_toma_el_candado_del_miembro()
    {
        await using var db = await ConTipoAsync();
        var gate = new PuertaDeFondosDePrueba(db);

        (await new GrantTokensHandler(db, Usuario(), Reloj(), gate).Handle(Conceder(), default)).IsSuccess.Should().BeTrue();

        gate.Entrados.Should().Equal("AMB-001");
    }

    [Fact]
    public async Task Conceder_con_el_candado_ocupado_no_escribe_nada()
    {
        await using var db = await ConTipoAsync();

        var r = await new GrantTokensHandler(db, Usuario(), Reloj(), new PuertaDeFondosDePrueba(db, "AMB-001"))
            .Handle(Conceder(), default);

        r.ErrorCode.Should().Be(TokenPurchaseErrors.Busy);
        db.TokenTransactions.Should().BeEmpty();
        db.TokenBalances.Should().BeEmpty();
    }

    [Fact]
    public async Task Ajustar_el_saldo_toma_el_candado_y_ocupado_no_lo_cambia()
    {
        await using var db = await ConTipoAsync();
        var balance = new TokenBalance { MemberId = "AMB-001", TokenTypeId = 1, Balance = 10, CreatedBy = "seed", CreationDate = Now };
        db.TokenBalances.Add(balance);
        await db.SaveChangesAsync();

        var gate = new PuertaDeFondosDePrueba(db);
        (await new UpdateTokenBalanceHandler(db, Usuario(), Reloj(), gate)
            .Handle(new UpdateTokenBalanceCommand(balance.Id, new AdminUpdateTokenBalanceRequest { Balance = 7 }), default))
            .IsSuccess.Should().BeTrue();
        gate.Entrados.Should().Equal("AMB-001");

        var r = await new UpdateTokenBalanceHandler(db, Usuario(), Reloj(), new PuertaDeFondosDePrueba(db, "AMB-001"))
            .Handle(new UpdateTokenBalanceCommand(balance.Id, new AdminUpdateTokenBalanceRequest { Balance = 99 }), default);
        r.ErrorCode.Should().Be(TokenPurchaseErrors.Busy);
        db.ChangeTracker.Clear();
        db.TokenBalances.Single().Balance.Should().Be(7);
    }

    [Fact]
    public async Task El_controlador_devuelve_409_si_esta_ocupado()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GrantTokensCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AdminTokenBalanceDto>.Failure(TokenPurchaseErrors.Busy, "busy"));
        mediator.Setup(m => m.Send(It.IsAny<UpdateTokenBalanceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AdminTokenBalanceDto>.Failure(TokenPurchaseErrors.Busy, "busy"));
        var controller = new AdminTokensController(mediator.Object);

        (await controller.GrantTokens(Conceder().Request, default)).Should().BeOfType<ConflictObjectResult>();
        (await controller.UpdateTokenBalance("b-1", new AdminUpdateTokenBalanceRequest { Balance = 1 }, default))
            .Should().BeOfType<ConflictObjectResult>();
    }
}
