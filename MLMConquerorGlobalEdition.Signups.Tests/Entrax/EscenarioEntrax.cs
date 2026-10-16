using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Un aspirante ruso con la fase 1 y los productos hechos (miembro, suscripción y usuario pendientes)
/// y las piezas para activarlo. Lo comparten las pruebas del alta con Entrax.
/// </summary>
internal static class EscenarioEntrax
{
    /// <summary>Hora de servidor (Kind sin especificar, como DateTime.Now guardado en SQL).</summary>
    public static readonly DateTime Ahora = new(2026, 10, 16, 10, 0, 0);

    public const string  OrderId   = "ORD-RU-1";
    public const string  MemberId  = "AMB-RU-1";
    public const string  Email     = "ivan@example.com";
    public const string  SubId     = "SUB-RU-1";
    public const decimal Total     = 80m;
    public const string  PaymentId = "550e8400-e29b-41d4-a716-446655440000";

    public static string ExternalOrderId(int intento = 1) => EntraxPayment.BuildExternalOrderId(OrderId, intento);

    public static Mock<IDateTimeProvider> Reloj(DateTime? ahora = null)
    {
        var t = ahora ?? Ahora;
        var m = new Mock<IDateTimeProvider>();
        m.Setup(c => c.Now).Returns(t);
        m.Setup(c => c.UtcNow).Returns(DateTime.SpecifyKind(t, DateTimeKind.Utc));
        return m;
    }

    public static EntraxOptions Opciones(Action<EntraxOptions>? ajustar = null)
    {
        var o = new EntraxOptions
        {
            Enabled = true, KeyId = "key-1", Secret = "secreto-de-pruebas",
            CallbackUrl = "https://alta.pruebas/api/v1/signups/entrax/callback"
        };
        ajustar?.Invoke(o);
        return o;
    }

    public static async Task SembrarAltaAsync(
        AppDbContext db, OrderStatus estadoDelPedido = OrderStatus.Pending, decimal total = Total, string pais = "RU")
    {
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = MemberId, Email = Email, FirstName = "Ivan", LastName = "Petrov",
            DateOfBirth = new DateTime(1990, 1, 1), MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Pending, EnrollDate = Ahora, Country = pais,
            CreatedBy = Email, CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.Products.Add(new Product
        {
            Id = "P-RU", Name = "Pack", Description = "Pack", ImageUrl = "https://cdn.example.com/p.png",
            MonthlyFee = total, SetupFee = 0, QualificationPoins = 10, IsActive = true,
            CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.Orders.Add(new Orders
        {
            Id = OrderId, MemberId = MemberId, OrderNo = "TAE1016RU", TotalAmount = total, Status = estadoDelPedido,
            OrderDate = Ahora, CreatedBy = Email, CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.OrderDetails.Add(new OrderDetail
        {
            OrderId = OrderId, ProductId = "P-RU", Quantity = 1, UnitPrice = total, CreatedBy = Email, CreationDate = Ahora
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = SubId, MemberId = MemberId, MembershipLevelId = 1, ChangeReason = SubscriptionChangeReason.New,
            SubscriptionStatus = MembershipStatus.Pending, StartDate = Ahora, IsAutoRenew = true,
            CreatedBy = Email, CreationDate = Ahora, LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();
    }

    public static async Task<EntraxPayment> SembrarPagoAsync(
        AppDbContext db,
        EntraxPaymentStatus estado = EntraxPaymentStatus.AwaitingPayment,
        string? paymentId = PaymentId,
        int intento = 1,
        decimal importe = Total,
        DateTime? caduca = null,
        DateTime? consultado = null,
        DateTime? creado = null)
    {
        var p = new EntraxPayment
        {
            OrderId = OrderId, MemberId = MemberId, ExternalOrderId = ExternalOrderId(intento),
            ExternalPaymentId = paymentId, Status = estado, AmountUsd = importe,
            AmountRub = importe * 95.5m, ExchangeRate = 95.5m,
            ExpiresAt = caduca ?? Ahora.AddMinutes(15), LastCheckedAt = consultado,
            CreatedBy = Email, CreationDate = creado ?? Ahora, LastUpdateDate = creado ?? Ahora
        };
        db.EntraxPayments.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public static ApplicationUser UsuarioInactivo() => new()
    {
        Id = Guid.NewGuid().ToString(), UserName = Email, NormalizedUserName = Email.ToUpperInvariant(),
        Email = Email, NormalizedEmail = Email.ToUpperInvariant(), EmailConfirmed = false,
        MemberProfileId = MemberId, IsActive = false, CreationDate = Ahora, CreatedBy = Email
    };

    public static Mock<UserManager<ApplicationUser>> Usuarios(ApplicationUser usuario)
    {
        var m = UserManagerHelper.Create();
        m.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(usuario);
        m.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        return m;
    }

    /// <summary>La activación de verdad, con los bonos y el cobro recurrente de mentira.</summary>
    public static SignupActivationService Activacion(AppDbContext db)
        => new(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
               Mock.Of<IRecurringBillingEnrollmentService>());

    public static EntraxPaymentState Remoto(
        string status, decimal original = Total, string currency = "USD", string? orderId = null, string paymentId = PaymentId)
        => new(paymentId, status, orderId ?? ExternalOrderId(), original, currency, original * 95.5m, 95.5m,
               status == "paid" ? new DateTimeOffset(Ahora.AddMinutes(5)) : null);

    public static EntraxCall<T> Ok<T>(T valor) where T : class => EntraxCall<T>.Success(valor, 200, 12);
}

/// <summary>Registro en memoria para comprobar que algo se avisó como Critical.</summary>
internal sealed class RegistroDePrueba<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Texto)> Entradas { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entradas.Add((logLevel, formatter(state, exception)));
}
