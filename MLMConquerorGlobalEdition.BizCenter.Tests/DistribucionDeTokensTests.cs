using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;
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

    // Raíz explícita: un contexto con interceptores ve la misma base que el resto.
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly string _name = Guid.NewGuid().ToString();
    private readonly DbContextOptions<AppDbContext> _options;

    private readonly AppDbContext _db;

    public DistribucionDeTokensTests()
    {
        _options = Opciones();
        _db = new(_options);
    }

    private DbContextOptions<AppDbContext> Opciones(params IInterceptor[] interceptores) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_name, _root)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptores)
            .Options;

    public void Dispose() => _db.Dispose();

    private DistributeTokenHandler Handler(IMemberFundsGate? gate = null, AppDbContext? db = null)
    {
        db ??= _db;
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
        return new DistributeTokenHandler(db, user.Object, clock.Object, cache.Object, push.Object,
            gate ?? new PuertaDeFondosDePrueba(db));
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

    private static TokensController Controlador(bool suplantando, IMediator? mediator = null, string? clave = null)
    {
        var ctx = new DefaultHttpContext();
        if (clave is not null) ctx.Request.Headers[TokenDistributionErrors.IdempotencyHeader] = clave;
        var claims = new List<Claim> { new("memberId", "AMB-1"), new(ClaimTypes.NameIdentifier, "user-001") };
        if (suplantando) claims.Add(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        return new TokensController(mediator ?? new Mock<IMediator>(MockBehavior.Strict).Object, Mock.Of<IBillingClient>())
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    [Fact]
    public async Task Bajo_suplantacion_no_se_distribuye_ni_se_transfiere()
    {
        var distribuir = await Controlador(suplantando: true, clave: "clave01").DistributeToken(
            new DistributeTokenRequest { TokenTypeId = Type, RecipientMemberId = Downline, Quantity = 1 },
            new Mock<ICacheService>(MockBehavior.Strict).Object, default);
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

    // ─── Revisión de la Tarea 14 ──────────────────────────────────────────────

    private async Task MiembroAsync(string id, string path, MemberAccountStatus status = MemberAccountStatus.Active, DateTime? nodo = null)
    {
        _db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = id, FirstName = "X", LastName = "X", MemberType = MemberType.Ambassador, EnrollDate = Now,
            Country = "US", Status = status, CreatedBy = "seed", LastUpdateDate = Now
        });
        _db.GenealogyTree.Add(new GenealogyEntity
        {
            MemberId = id, ParentMemberId = "X", HierarchyPath = path, Level = path.Count(c => c == '/') - 1,
            CreatedBy = "seed", CreationDate = nodo ?? Now
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Un_nieto_es_del_equipo()
    {
        await SeedAsync(I(1, "OLD0001", 9));
        await MiembroAsync("AMB-NIETO", $"/{Sender}/{Downline}/AMB-NIETO/");

        var r = await Handler().Handle(Cmd("AMB-NIETO", 1), default);

        r.IsSuccess.Should().BeTrue(r.Error);
        (await InstanciasAsync()).Single().MemberId.Should().Be("AMB-NIETO");
    }

    [Fact]
    public async Task Un_id_que_empieza_igual_no_es_del_equipo()
    {
        // /AMB-SENDER/ no es prefijo de /AMB-SENDER2/ (el caso /S/ frente a /S2/).
        await SeedAsync(I(1, "OLD0001", 9));
        await MiembroAsync(Sender + "2", $"/{Sender}2/");

        var r = await Handler().Handle(Cmd(Sender + "2", 1), default);

        r.ErrorCode.Should().Be("RECIPIENT_NOT_IN_DOWNLINE");
    }

    [Fact]
    public async Task Una_ruta_guardada_sin_barra_final_se_normaliza()
    {
        await SeedAsync(I(1, "OLD0001", 9));
        var nodo = await _db.GenealogyTree.SingleAsync(g => g.MemberId == Sender);
        nodo.HierarchyPath = $"/{Sender}";
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        await MiembroAsync(Sender + "2", $"/{Sender}2/");

        var r = await Handler().Handle(Cmd(Sender + "2", 1), default);

        r.ErrorCode.Should().Be("RECIPIENT_NOT_IN_DOWNLINE", "sin la barra, /AMB-SENDER sería prefijo de /AMB-SENDER2/");
    }

    [Fact]
    public async Task Con_dos_nodos_del_que_envia_manda_el_mas_reciente()
    {
        await SeedAsync(I(1, "OLD0001", 9));
        // Un nodo viejo en otra rama que sí contendría al de fuera: no debe contar.
        _db.GenealogyTree.Add(new GenealogyEntity
        {
            MemberId = Sender, ParentMemberId = "ROOT", HierarchyPath = $"/{Outsider}/{Sender}/", Level = 2,
            CreatedBy = "seed", CreationDate = Now.AddYears(-3)
        });
        _db.GenealogyTree.Single(g => g.MemberId == Outsider).HierarchyPath = $"/{Outsider}/{Sender}/{Outsider}/";
        await _db.SaveChangesAsync();
        // El contexto pone la fecha de alta al guardar: se fija a mano para que este nodo sea el viejo.
        _db.GenealogyTree.Single(g => g.HierarchyPath == $"/{Outsider}/{Sender}/").CreationDate = Now.AddYears(-3);
        _db.GenealogyTree.Single(g => g.HierarchyPath == $"/{Sender}/").CreationDate = Now;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var r = await Handler().Handle(Cmd(Outsider, 1), default);

        r.ErrorCode.Should().Be("RECIPIENT_NOT_IN_DOWNLINE");
    }

    [Theory]
    [InlineData(MemberAccountStatus.Inactive)]
    [InlineData(MemberAccountStatus.Suspended)]
    [InlineData(MemberAccountStatus.Terminated)]
    [InlineData(MemberAccountStatus.Pending)]
    public async Task Un_destinatario_sin_cuenta_activa_se_rechaza_al_distribuir_y_al_transferir(MemberAccountStatus estado)
    {
        await SeedAsync(I(1, "OLD0001", 9));
        await MiembroAsync("AMB-DOWN-2", $"/{Sender}/AMB-DOWN-2/", estado);

        var distribuir = await Handler().Handle(Cmd("AMB-DOWN-2", 1), default);
        var transferir = await Transferir().Handle(new TransferTokenInstanceCommand("OLD0001", "AMB-DOWN-2"), default);

        distribuir.ErrorCode.Should().Be(TokenDistributionErrors.RecipientNotActive);
        transferir.ErrorCode.Should().Be(TokenDistributionErrors.RecipientNotActive);
        (await InstanciasAsync()).Single().MemberId.Should().Be(Sender);
    }

    private TransferTokenInstanceHandler Transferir(AppDbContext? db = null, string userId = "user-sender")
    {
        db ??= _db;
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.MemberId).Returns(Sender);
        user.Setup(u => u.UserId).Returns(userId);
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new TransferTokenInstanceHandler(db, user.Object, clock.Object, cache.Object, push.Object,
            NullLogger<TransferTokenInstanceHandler>.Instance, new PuertaDeFondosDePrueba(db));
    }

    [Fact]
    public async Task Transferir_deja_el_codigo_en_la_nota_y_firma_con_el_usuario()
    {
        await SeedAsync(I(1, "OLD0001", 9));

        var r = await Transferir(userId: "user-9").Handle(
            new TransferTokenInstanceCommand("OLD0001", Downline, "para tu primo"), default);

        r.IsSuccess.Should().BeTrue(r.Error);
        var asiento = await _db.TokenTransactions.AsNoTracking().SingleAsync(t => t.ReferenceId == null);
        asiento.Notes.Should().Be($"Token OLD0001 → {Downline}. para tu primo");
        asiento.CreatedBy.Should().Be("user-9");
    }

    [Fact]
    public async Task Distribuir_firma_con_el_usuario()
    {
        await SeedAsync(I(1, "OLD0001", 9));

        (await Handler().Handle(Cmd(Downline, 1), default)).IsSuccess.Should().BeTrue();

        (await _db.TokenTransactions.AsNoTracking().SingleAsync(t => t.ReferenceId == null))
            .CreatedBy.Should().Be("user-sender");
    }

    /// <summary>
    /// Antes del primer guardado, otro proceso (que no toma este candado, como un canje en el alta con
    /// el dueño de antes) canjea el código: el guardado debe chocar con el token de concurrencia.
    /// </summary>
    private sealed class OtroLoCanjeaAntesDeGuardar(DbContextOptions<AppDbContext> otra, string code) : SaveChangesInterceptor
    {
        private bool _hecho;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!_hecho)
            {
                _hecho = true;
                await using var db = new AppDbContext(otra);
                var x = await db.TokenTransactions.SingleAsync(t => t.ReferenceId == code, ct);
                x.Status         = TokenInstanceStatus.Used;
                x.UsedByMemberId = "AMB-NUEVO";
                await db.SaveChangesAsync(ct);
            }
            return result;
        }
    }

    [Fact]
    public async Task Si_el_guardado_choca_con_otro_que_canjeo_el_codigo_distribuir_es_ocupado_y_no_mueve_nada()
    {
        await SeedAsync(I(1, "OLD0001", 9));
        await using var conGancho = new AppDbContext(Opciones(new OtroLoCanjeaAntesDeGuardar(_options, "OLD0001")));

        var r = await Handler(db: conGancho).Handle(Cmd(Downline, 1), default);

        r.ErrorCode.Should().Be(TokenPurchaseErrors.Busy);
        conGancho.ChangeTracker.Entries().Should().BeEmpty();
        var codigo = (await InstanciasAsync()).Single();
        codigo.MemberId.Should().Be(Sender, "no se movió un código ya canjeado");
        codigo.Status.Should().Be(TokenInstanceStatus.Used);
    }

    [Fact]
    public async Task Si_el_guardado_choca_con_otro_que_canjeo_el_codigo_transferir_es_ocupado()
    {
        await SeedAsync(I(1, "OLD0001", 9));
        await using var conGancho = new AppDbContext(Opciones(new OtroLoCanjeaAntesDeGuardar(_options, "OLD0001")));

        var r = await Transferir(conGancho).Handle(new TransferTokenInstanceCommand("OLD0001", Downline), default);

        r.ErrorCode.Should().Be(TokenPurchaseErrors.Busy);
        (await InstanciasAsync()).Single().Status.Should().Be(TokenInstanceStatus.Used);
    }

    // ─── Controlador: idempotencia y 409 ─────────────────────────────────────

    private static DistributeTokenRequest Peticion() => new() { TokenTypeId = Type, RecipientMemberId = Downline, Quantity = 1 };

    private static Mock<ICacheService> Cache(bool reclama = true)
    {
        var c = new Mock<ICacheService>();
        c.Setup(x => x.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(reclama);
        c.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return c;
    }

    private static Mock<IMediator> Mediador(Result<bool> resultado)
    {
        var m = new Mock<IMediator>();
        m.Setup(x => x.Send(It.IsAny<DistributeTokenCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(resultado);
        return m;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("clave con espacios")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public async Task Sin_clave_de_idempotencia_valida_es_400(string? clave)
    {
        var r = await Controlador(false, clave: clave).DistributeToken(
            Peticion(), new Mock<ICacheService>(MockBehavior.Strict).Object, default);

        var o = r.Should().BeOfType<BadRequestObjectResult>().Subject;
        o.Value.Should().BeOfType<ApiResponse<bool>>().Which.ErrorCode.Should().Be(TokenPurchaseErrors.IdempotencyKeyRequired);
    }

    [Fact]
    public async Task Reclama_la_clave_por_miembro_24_horas_antes_de_distribuir()
    {
        var cache = Cache();
        var mediator = Mediador(Result<bool>.Success(true));

        var r = await Controlador(false, mediator.Object, "clave01").DistributeToken(Peticion(), cache.Object, default);

        r.Should().BeOfType<OkObjectResult>();
        cache.Verify(c => c.TryClaimOnceAsync("dist:AMB-1:clave01", TimeSpan.FromHours(24), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "una distribución hecha deja la clave reclamada");
    }

    [Fact]
    public async Task La_misma_clave_otra_vez_es_409_y_no_distribuye()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);

        var r = await Controlador(false, mediator.Object, "clave01").DistributeToken(Peticion(), Cache(reclama: false).Object, default);

        var o = r.Should().BeOfType<ConflictObjectResult>().Subject;
        o.Value.Should().BeOfType<ApiResponse<bool>>().Which.ErrorCode.Should().Be(TokenPurchaseErrors.IdempotencyKeyReused);
    }

    [Fact]
    public async Task Con_la_cache_caida_falla_cerrado_con_503()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(x => x.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("redis caído"));
        var mediator = new Mock<IMediator>(MockBehavior.Strict);

        var r = await Controlador(false, mediator.Object, "clave01").DistributeToken(Peticion(), cache.Object, default);

        var o = r.Should().BeOfType<ObjectResult>().Subject;
        o.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        o.Value.Should().BeOfType<ApiResponse<bool>>().Which.ErrorCode.Should().Be(TokenDistributionErrors.IdempotencyUnavailable);
    }

    [Fact]
    public async Task Un_fallo_con_codigo_suelta_la_clave_para_poder_repetir()
    {
        var cache = Cache();
        var mediator = Mediador(Result<bool>.Failure("INSUFFICIENT_TOKEN_BALANCE", "no"));

        var r = await Controlador(false, mediator.Object, "clave01").DistributeToken(Peticion(), cache.Object, default);

        r.Should().BeOfType<BadRequestObjectResult>();
        cache.Verify(c => c.RemoveAsync("dist:AMB-1:clave01", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ocupado_es_409_al_distribuir_y_al_transferir()
    {
        var distribuir = await Controlador(false, Mediador(Result<bool>.Failure(TokenPurchaseErrors.Busy, "busy")).Object, "clave01")
            .DistributeToken(Peticion(), Cache().Object, default);

        var m = new Mock<IMediator>();
        m.Setup(x => x.Send(It.IsAny<TransferTokenInstanceCommand>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<TransferTokenInstanceResponse>.Failure(TokenPurchaseErrors.Busy, "busy"));
        var transferir = await Controlador(false, m.Object).TransferInstance(
            new TransferTokenInstanceRequest { TokenCode = "OLD0001", RecipientMemberId = Downline }, default);

        foreach (var r in new[] { distribuir, transferir })
            r.Should().BeOfType<ConflictObjectResult>()
             .Which.Value.Should().BeOfType<ApiResponse<object>>()
             .Which.ErrorCode.Should().Be(TokenPurchaseErrors.Busy);
    }
}
