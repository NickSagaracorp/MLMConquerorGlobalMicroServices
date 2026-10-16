using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// El recibo lleva lo que el miembro necesita para reconocer la compra, en su idioma, y nunca los
/// códigos (son al portador). Si el correo falla, la compra sigue hecha.
/// </summary>
public class TokenPurchaseReceiptsTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 30, 0);

    private static (TokenPurchaseReceipts Sut, Mock<IEmailService> Email) Build(string lang = "es")
    {
        var db = TestDbContextFactory.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = "AMB-1", Email = "ana@example.com", FirstName = "Ana", LastName = "Ruiz",
            DefaultLanguage = lang, MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US",
            CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
        });
        db.SaveChanges();
        var email = new Mock<IEmailService>();
        return (new TokenPurchaseReceipts(db, email.Object, NullLogger<TokenPurchaseReceipts>.Instance), email);
    }

    private static TokenPurchase Compra() => new()
    {
        Id = "purchase-1", MemberId = "AMB-1", TokenTypeId = 13, Quantity = 2, UnitPrice = 99.99m,
        TotalAmount = 199.98m, PaymentMethod = TokenPaymentMethod.CommissionBalance,
        Status = TokenPurchaseStatus.Completed, CompletedAt = Now
    };

    [Fact]
    public async Task Manda_el_recibo_con_los_datos_de_la_compra_y_sin_codigos()
    {
        var (sut, email) = Build();
        Dictionary<string, string>? vars = null;
        email.Setup(e => e.SendAsync("ana@example.com", "Ana Ruiz", "es", NotificationEvents.TokenPurchaseReceipt,
                                     It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
             .Callback<string, string, string, string, Dictionary<string, string>, CancellationToken>((_, _, _, _, v, _) => vars = v)
             .Returns(Task.CompletedTask);

        await sut.SendAsync(Compra(), "TP1015-1", "Enrollment: VIP Member");

        vars!["TokenType"].Should().Be("Enrollment: VIP Member");
        vars["Quantity"].Should().Be("2");
        vars["Amount"].Should().Be("199.98 USD");
        vars["PaymentMethod"].Should().Be("saldo de comisiones");
        vars["PurchaseDate"].Should().Be("2026-10-15 10:30");
        vars["PurchaseId"].Should().Be("purchase-1");
        vars["OrderNo"].Should().Be("TP1015-1");
        vars.Keys.Should().NotContain(k => k.Contains("Code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Si_el_correo_falla_no_lanza()
    {
        var (sut, email) = Build("en");
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                     It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("SES caido"));

        var act = () => sut.SendAsync(Compra(), "TP1", "VIP");

        await act.Should().NotThrowAsync();
    }
}
