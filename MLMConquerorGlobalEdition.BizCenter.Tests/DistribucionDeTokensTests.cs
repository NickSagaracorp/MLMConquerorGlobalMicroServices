using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// La puerta de fondos en pruebas: la transacción (no-op en InMemory) sin el candado de SQL Server.
/// Anota por qué miembros se pasó, puede declarar ocupados a algunos y puede ejecutar algo justo al
/// entrar, que es lo que haría otra operación que terminó mientras esta esperaba el candado. La
/// exclusión mutua real la prueban SqlServerMemberFundsGateTests (Billing.Tests).
/// </summary>
public sealed class PuertaDeFondosDePrueba(AppDbContext db, params string[] ocupados) : IMemberFundsGate
{
    public List<string> Entrados { get; } = new();
    public Func<Task>? AlEntrar { get; set; }

    public async Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
    {
        if (ocupados.Contains(memberId)) throw new MemberFundsBusyException(memberId);
        Entrados.Add(memberId);
        var tx = await db.Database.BeginTransactionAsync(ct);
        if (AlEntrar is not null) await AlEntrar();
        return tx;
    }

    public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default)
    {
        if (ocupados.Contains(memberId)) return Task.FromResult(false);
        Entrados.Add(memberId);
        return Task.FromResult(true);
    }
}

/// <summary>
/// DISTRIBUIR ES MOVER, NO DUPLICAR. El handler viejo restaba del saldo del que enviaba y acuñaba
/// códigos nuevos para el que recibía, pero dejaba los códigos del que enviaba en Issued: los dos
/// podían canjearlos. Y no miraba si el destinatario era de su equipo. Ahora pasa los mismos códigos
/// (los más antiguos primero), solo al equipo de inscripción, y bajo el candado de fondos de los dos
/// miembros para que dos distribuciones a la vez no muevan el mismo código ni dejen saldo negativo.
/// </summary>
public class DistribucionDeTokensTests : IDisposable
{
    private const string Sender   = "AMB-SENDER";
    private const string Downline = "AMB-DOWN-1";
    private const string Outsider = "AMB-OTHER";
    private const int    Type     = 13;
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private readonly DbContextOptions<AppDbContext> _options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    private readonly AppDbContext _db;

    public DistribucionDeTokensTests() => _db = new(_options);

    public void Dispose() => _db.Dispose();

    private DistributeTokenHandler Handler(IMemberFundsGate? gate = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.MemberId).Returns(Sender);
        user.Setup(u => u.UserId).Returns("user-sender");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new DistributeTokenHandler(_db, user.Object, clock.Object, cache.Object, push.Object,
            gate ?? new PuertaDeFondosDePrueba(_db));
    }

    private async Task SeedAsync(int? senderBalance = null, params TokenTransaction[] instances)
    {
        _db.MemberProfiles.AddRange(
            new MemberProfile { MemberId = Sender,   FirstName = "S", LastName = "S", MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US", CreatedBy = "seed", LastUpdateDate = Now },
            new MemberProfile { MemberId = Downline, FirstName = "D", LastName = "D", MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US", CreatedBy = "seed", LastUpdateDate = Now },
            new MemberProfile { MemberId = Outsider, FirstName = "O", LastName = "O", MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US", CreatedBy = "seed", LastUpdateDate = Now });
        _db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = Sender,   ParentMemberId = "ROOT", HierarchyPath = $"/{Sender}/",             Level = 1, CreatedBy = "seed", CreationDate = Now },
            new GenealogyEntity { MemberId = Downline, ParentMemberId = Sender, HierarchyPath = $"/{Sender}/{Downline}/", Level = 2, CreatedBy = "seed", CreationDate = Now },
            new GenealogyEntity { MemberId = Outsider, ParentMemberId = "ROOT", HierarchyPath = $"/{Outsider}/",           Level = 1, CreatedBy = "seed", CreationDate = Now });
        _db.TokenTypes.Add(new TokenType { Id = Type, Name = "VIP Member", IsActive = true, CreatedBy = "seed", CreationDate = Now });
        _db.TokenTransactions.AddRange(instances);
        _db.TokenBalances.Add(new TokenBalance
        {
            Id = Guid.NewGuid().ToString(), MemberId = Sender, TokenTypeId = Type,
            Balance = senderBalance ?? instances.Length, CreatedBy = "seed", CreationDate = Now
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private Task SeedAsync(params TokenTransaction[] instances) => SeedAsync(null, instances);

    private static TokenTransaction I(long id, string code, int daysAgo,
        TokenInstanceStatus status = TokenInstanceStatus.Issued, DateTime? expires = null) => new()
    {
        Id = id, MemberId = Sender, OriginalOwnerMemberId = Sender, TokenTypeId = Type,
        TransactionType = TokenTransactionType.Purchased, Quantity = 1, ReferenceId = code,
        Status = status, ExpiresAt = expires, CreatedBy = "seed", CreationDate = Now.AddDays(-daysAgo)
    };

    private static DistributeTokenCommand Cmd(string to, int qty) =>
        new(new DistributeTokenRequest { TokenTypeId = Type, RecipientMemberId = to, Quantity = qty });

    private async Task<List<TokenTransaction>> InstanciasAsync() =>
        await _db.TokenTransactions.AsNoTracking().Where(t => t.ReferenceId != null).ToListAsync();

    [Fact]
    public async Task Mueve_los_codigos_mas_antiguos_sin_crear_nuevos()
    {
        await SeedAsync(I(1, "OLD0001", 9), I(2, "OLD0002", 5), I(3, "NEW0003", 1));

        var r = await Handler().Handle(Cmd(Downline, 2), default);

        r.IsSuccess.Should().BeTrue();
        var instancias = await InstanciasAsync();
        instancias.Should().HaveCount(3, "distribuir no acuña códigos");
        instancias.Where(t => t.MemberId == Downline).Select(t => t.ReferenceId)
                  .Should().BeEquivalentTo(new[] { "OLD0001", "OLD0002" });
        instancias.Where(t => t.MemberId == Downline)
                  .Should().OnlyContain(t => t.PreviousOwnerMemberId == Sender
                                          && t.OriginalOwnerMemberId == Sender
                                          && t.Status == TokenInstanceStatus.Distributed);
        instancias.Single(t => t.ReferenceId == "NEW0003").MemberId.Should().Be(Sender);
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Sender)).Balance.Should().Be(1);
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Downline)).Balance.Should().Be(2);

        var asiento = await _db.TokenTransactions.SingleAsync(t => t.ReferenceId == null);
        asiento.Quantity.Should().Be(2);
        asiento.TransactionType.Should().Be(TokenTransactionType.Distributed);
        asiento.MemberId.Should().Be(Sender);
        asiento.DistributedToMemberId.Should().Be(Downline);
        asiento.Notes.Should().Contain("OLD0001").And.Contain("OLD0002");
    }

    [Fact]
    public async Task Fuera_del_equipo_se_rechaza_y_no_mueve_nada()
    {
        await SeedAsync(I(1, "OLD0001", 9));

        var r = await Handler().Handle(Cmd(Outsider, 1), default);

        r.ErrorCode.Should().Be("RECIPIENT_NOT_IN_DOWNLINE");
        (await _db.TokenTransactions.SingleAsync()).MemberId.Should().Be(Sender);
    }

    [Fact]
    public async Task Hacia_arriba_tampoco_es_el_equipo()
    {
        await SeedAsync();
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.MemberId).Returns(Downline);
        user.Setup(u => u.UserId).Returns("user-down");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        var handler = new DistributeTokenHandler(_db, user.Object, clock.Object,
            Mock.Of<ICacheService>(), Mock.Of<IPushNotificationService>(), new PuertaDeFondosDePrueba(_db));

        var r = await handler.Handle(Cmd(Sender, 1), default);

        r.ErrorCode.Should().Be("RECIPIENT_NOT_IN_DOWNLINE", "el patrocinador no está debajo de quien envía");
    }

    [Fact]
    public async Task No_cuenta_usados_ni_vencidos_como_disponibles()
    {
        await SeedAsync(
            I(1, "USED001", 9, TokenInstanceStatus.Used),
            I(2, "EXPI002", 8, expires: Now.AddDays(-1)),
            I(3, "GOOD003", 1));

        var r = await Handler().Handle(Cmd(Downline, 2), default);

        r.ErrorCode.Should().Be("INSUFFICIENT_TOKEN_BALANCE");
        _db.TokenTransactions.Count(t => t.MemberId == Downline).Should().Be(0);
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Sender)).Balance.Should().Be(3);
    }

    [Fact]
    public async Task Toma_el_candado_de_los_dos_miembros_en_orden_ordinal()
    {
        await SeedAsync(I(1, "OLD0001", 9));
        var gate = new PuertaDeFondosDePrueba(_db);

        var r = await Handler(gate).Handle(Cmd(Downline, 1), default);

        r.IsSuccess.Should().BeTrue();
        gate.Entrados.Should().Equal(Downline, Sender);
    }

    [Theory]
    [InlineData(Sender)]
    [InlineData(Downline)]
    public async Task Con_el_candado_ocupado_no_mueve_nada(string ocupado)
    {
        await SeedAsync(I(1, "OLD0001", 9));

        var r = await Handler(new PuertaDeFondosDePrueba(_db, ocupado)).Handle(Cmd(Downline, 1), default);

        r.ErrorCode.Should().Be(TokenPurchaseErrors.Busy);
        (await InstanciasAsync()).Single().MemberId.Should().Be(Sender);
        _db.TokenTransactions.Count(t => t.ReferenceId == null).Should().Be(0);
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Sender)).Balance.Should().Be(1);
    }

    [Fact]
    public async Task Lee_los_codigos_despues_de_tomar_el_candado()
    {
        // Otra distribución terminó mientras esta esperaba el candado y se llevó OLD0001.
        await SeedAsync(I(1, "OLD0001", 9), I(2, "OLD0002", 5), I(3, "NEW0003", 1));
        var gate = new PuertaDeFondosDePrueba(_db);
        gate.AlEntrar = async () =>
        {
            await using var otra = new AppDbContext(_options);
            var x = await otra.TokenTransactions.SingleAsync(t => t.ReferenceId == "OLD0001");
            x.MemberId = Downline;
            x.Status   = TokenInstanceStatus.Distributed;
            await otra.SaveChangesAsync();
        };

        var r = await Handler(gate).Handle(Cmd(Downline, 2), default);

        r.IsSuccess.Should().BeTrue();
        var instancias = await InstanciasAsync();
        instancias.Where(t => t.MemberId == Sender).Should().BeEmpty();
        instancias.Single(t => t.ReferenceId == "OLD0001").PreviousOwnerMemberId
                  .Should().BeNull("no se volvió a mover el código que ya se había llevado la otra");
    }

    [Fact]
    public async Task Dos_distribuciones_seguidas_no_mueven_el_mismo_codigo_ni_dejan_saldo_negativo()
    {
        await SeedAsync(I(1, "OLD0001", 9), I(2, "OLD0002", 5), I(3, "NEW0003", 1));

        var primera = await Handler().Handle(Cmd(Downline, 2), default);
        _db.ChangeTracker.Clear();
        var segunda = await Handler().Handle(Cmd(Downline, 2), default);

        primera.IsSuccess.Should().BeTrue();
        segunda.ErrorCode.Should().Be("INSUFFICIENT_TOKEN_BALANCE");
        (await InstanciasAsync()).Count(t => t.MemberId == Downline).Should().Be(2);
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Sender)).Balance.Should().Be(1);
    }

    [Fact]
    public async Task Un_saldo_agregado_desfasado_no_queda_negativo()
    {
        await SeedAsync(senderBalance: 0, I(1, "OLD0001", 9));

        var r = await Handler().Handle(Cmd(Downline, 1), default);

        r.IsSuccess.Should().BeTrue("los códigos mandan, el agregado es una caché");
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Sender)).Balance.Should().Be(0);
    }

    // ─── Controlador ─────────────────────────────────────────────────────────

    private static TokensController Controlador(bool suplantando)
    {
        var ctx = new DefaultHttpContext();
        var claims = new List<Claim> { new("memberId", "AMB-1"), new(ClaimTypes.NameIdentifier, "user-001") };
        if (suplantando) claims.Add(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        return new TokensController(new Mock<IMediator>(MockBehavior.Strict).Object, Mock.Of<IBillingClient>())
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    [Fact]
    public async Task Bajo_suplantacion_no_se_distribuye_ni_se_transfiere()
    {
        var distribuir = await Controlador(suplantando: true).DistributeToken(
            new DistributeTokenRequest { TokenTypeId = Type, RecipientMemberId = Downline, Quantity = 1 }, default);
        var transferir = await Controlador(suplantando: true).TransferInstance(
            new TransferTokenInstanceRequest { TokenCode = "OLD0001", RecipientMemberId = Downline }, default);

        foreach (var r in new[] { distribuir, transferir })
        {
            var o = r.Should().BeAssignableTo<ObjectResult>().Subject;
            o.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            o.Value.Should().BeOfType<ApiResponse<object>>()
             .Which.ErrorCode.Should().Be(TokenDistributionErrors.Impersonation);
        }
    }
}
