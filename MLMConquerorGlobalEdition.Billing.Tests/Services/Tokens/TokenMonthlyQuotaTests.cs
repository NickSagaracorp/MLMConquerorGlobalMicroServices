using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// El tope de 50 al mes: comprados emitidos en el mes (por dueño ORIGINAL) más compras en vuelo
/// del mismo mes, incluidas las que esperan conciliación.
/// </summary>
public class TokenMonthlyQuotaTests
{
    private const string Yo   = "AMB-1";
    private const string Otro = "AMB-2";
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static TokenTransaction Instancia(
        string code, DateTime at, TokenTransactionType tipo = TokenTransactionType.Purchased,
        string? dueño = null, bool conCodigo = true) => new()
    {
        MemberId = dueño ?? Yo, OriginalOwnerMemberId = Yo, TokenTypeId = 13,
        TransactionType = tipo, Quantity = 1, ReferenceId = conCodigo ? code : null,
        Status = TokenInstanceStatus.Issued, CreatedBy = "seed", CreationDate = at
    };

    private static TokenPurchase Compra(TokenPurchaseStatus status, int cantidad, int periodo = 202610) => new()
    {
        MemberId = Yo, TokenTypeId = 13, GrantedProductIds = "prod-vip", Quantity = cantidad,
        UnitPrice = 1, TotalAmount = cantidad, PaymentMethod = TokenPaymentMethod.Crypto,
        Status = status, OrderId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString("N"),
        PeriodKey = periodo, CreatedBy = "seed", CreationDate = Now
    };

    private static Task<int> Usado(AppDbContext db) => new TokenMonthlyQuota(db).UsedAsync(Yo, Now);

    /// <summary>
    /// AuditInterceptor pisa CreationDate con la hora de la máquina al insertar, así que la fecha de
    /// cada fila se vuelve a poner después: sin esto la prueba dependería del día en que se corre.
    /// </summary>
    private static async Task Sembrar(AppDbContext db, params TokenTransaction[] filas)
    {
        var fechas = filas.Select(f => f.CreationDate).ToArray();
        db.TokenTransactions.AddRange(filas);
        await db.SaveChangesAsync();
        for (var i = 0; i < filas.Length; i++) filas[i].CreationDate = fechas[i];
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Cuenta_los_comprados_del_mes_por_dueno_original_aunque_los_haya_pasado()
    {
        var db = TestDbContextFactory.Create();
        await Sembrar(db,
            Instancia("AAA1111", Now.AddDays(-1)),
            Instancia("AAA2222", Now.AddDays(-14)),
            Instancia("AAA3333", Now.AddDays(-2), dueño: Otro));

        (await Usado(db)).Should().Be(3);
    }

    [Fact]
    public async Task No_cuenta_otros_movimientos_ni_meses_anteriores_ni_filas_sin_codigo()
    {
        var db = TestDbContextFactory.Create();
        await Sembrar(db,
            Instancia("BBB1111", Now.AddDays(-1), TokenTransactionType.AdminGranted),
            Instancia("BBB2222", Now.AddMonths(-1)),
            Instancia("—", Now.AddDays(-1), conCodigo: false));

        (await Usado(db)).Should().Be(0);
    }

    [Fact]
    public async Task Suma_las_compras_en_vuelo_y_no_las_cerradas()
    {
        var db = TestDbContextFactory.Create();
        db.TokenPurchases.AddRange(
            Compra(TokenPurchaseStatus.Pending, 4),
            Compra(TokenPurchaseStatus.AwaitingCryptoConfirmation, 2),
            Compra(TokenPurchaseStatus.NeedsReconciliation, 3),
            Compra(TokenPurchaseStatus.Failed, 10),
            Compra(TokenPurchaseStatus.Completed, 7));
        await db.SaveChangesAsync();

        (await Usado(db)).Should().Be(9);
    }

    [Fact]
    public async Task Una_reserva_de_otro_mes_no_cuenta()
    {
        var db = TestDbContextFactory.Create();
        db.TokenPurchases.Add(Compra(TokenPurchaseStatus.Pending, 5, periodo: 202609));
        await db.SaveChangesAsync();

        (await Usado(db)).Should().Be(0);
    }

    // ── Una compra cuenta en un solo mes: el de su PeriodKey ───────────────────

    private static readonly DateTime Septiembre = new(2026, 9, 30, 18, 0, 0);

    [Fact]
    public async Task Cripto_reservada_en_septiembre_y_confirmada_en_octubre_cuenta_solo_en_septiembre()
    {
        var db = TestDbContextFactory.Create();
        var compra = Compra(TokenPurchaseStatus.AwaitingCryptoConfirmation, 50, periodo: 202609);
        db.TokenPurchases.Add(compra);
        await db.SaveChangesAsync();
        var quota = new TokenMonthlyQuota(db);

        // Esperando la transferencia: reserva septiembre, octubre está libre.
        (await quota.UsedAsync(Yo, Septiembre)).Should().Be(50);
        (await quota.UsedAsync(Yo, Now)).Should().Be(0);

        // El administrador confirma el 2 de octubre: los tokens nacen en octubre pero son de septiembre.
        compra.Complete("ph-1", Now, "admin@x.com");
        var emitidos = Enumerable.Range(0, 50)
            .Select(i => Instancia($"SEP{i:0000}", new DateTime(2026, 10, 2, 9, 0, 0)))
            .ToArray();
        foreach (var t in emitidos) t.TokenPurchaseId = compra.Id;
        await Sembrar(db, emitidos);

        (await quota.UsedAsync(Yo, Septiembre)).Should().Be(50);
        (await quota.UsedAsync(Yo, Now)).Should().Be(0, "octubre conserva su cupo entero: 50 más no pasan el tope de ningún mes");
    }

    [Fact]
    public async Task Lo_emitido_por_una_compra_de_este_mes_cuenta_aunque_se_haya_regalado()
    {
        var db = TestDbContextFactory.Create();
        var compra = Compra(TokenPurchaseStatus.Completed, 2);
        db.TokenPurchases.Add(compra);
        await db.SaveChangesAsync();
        var a = Instancia("CCC1111", Now.AddDays(-1));
        var b = Instancia("CCC2222", Now.AddDays(-1), dueño: Otro);
        a.TokenPurchaseId = b.TokenPurchaseId = compra.Id;
        await Sembrar(db, a, b);

        (await Usado(db)).Should().Be(2);
    }

    [Fact]
    public async Task Una_compra_cerrada_sin_emitir_no_cuenta_en_ningun_mes()
    {
        var db = TestDbContextFactory.Create();
        db.TokenPurchases.AddRange(
            Compra(TokenPurchaseStatus.Cancelled, 5),
            Compra(TokenPurchaseStatus.Rejected, 5),
            Compra(TokenPurchaseStatus.Expired, 5));
        await db.SaveChangesAsync();

        (await Usado(db)).Should().Be(0);
    }
}
