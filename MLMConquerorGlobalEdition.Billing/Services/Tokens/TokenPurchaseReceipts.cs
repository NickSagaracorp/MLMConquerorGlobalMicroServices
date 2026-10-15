using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public interface ITokenPurchaseReceipts
{
    /// <summary>Manda el recibo. No lanza: un fallo de correo se registra y la compra sigue hecha.</summary>
    Task SendAsync(TokenPurchase purchase, string? orderNo, string tokenTypeName, CancellationToken ct = default);
}

/// <summary>
/// Recibo de una compra completada o de una compra en cripto confirmada. Sin códigos de token: son
/// al portador y un buzón no es un lugar seguro para ellos; la pantalla los muestra al comprar.
/// </summary>
public sealed class TokenPurchaseReceipts : ITokenPurchaseReceipts
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<TokenPurchaseReceipts> _logger;

    public TokenPurchaseReceipts(AppDbContext db, IEmailService email, ILogger<TokenPurchaseReceipts> logger)
    {
        _db     = db;
        _email  = email;
        _logger = logger;
    }

    public async Task SendAsync(TokenPurchase purchase, string? orderNo, string tokenTypeName, CancellationToken ct = default)
    {
        try
        {
            var member = await _db.MemberProfiles.AsNoTracking()
                .Where(m => m.MemberId == purchase.MemberId)
                .Select(m => new { m.Email, m.FirstName, m.LastName, m.DefaultLanguage })
                .FirstOrDefaultAsync(ct);

            if (member is null || string.IsNullOrWhiteSpace(member.Email))
            {
                _logger.LogWarning("Recibo de compra {PurchaseId} sin enviar: el miembro no tiene correo.", purchase.Id);
                return;
            }

            var fullName = $"{member.FirstName} {member.LastName}".Trim();
            var language = string.IsNullOrWhiteSpace(member.DefaultLanguage) ? "en" : member.DefaultLanguage;

            await _email.SendAsync(member.Email, fullName, language, NotificationEvents.TokenPurchaseReceipt,
                new Dictionary<string, string>
                {
                    ["FullName"]      = fullName,
                    ["TokenType"]     = tokenTypeName,
                    ["Quantity"]      = purchase.Quantity.ToString(CultureInfo.InvariantCulture),
                    ["Amount"]        = purchase.TotalAmount.ToString("F2", CultureInfo.InvariantCulture) + " USD",
                    ["PaymentMethod"] = MethodText(purchase.PaymentMethod, language),
                    ["PurchaseDate"]  = (purchase.CompletedAt ?? purchase.CreationDate).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    ["PurchaseId"]    = purchase.Id,
                    ["OrderNo"]       = orderNo ?? purchase.OrderId
                }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el recibo de la compra de tokens {PurchaseId}.", purchase.Id);
        }
    }

    /// <summary>La plantilla solo existe en en/es; el resto de idiomas cae a inglés en el transporte.</summary>
    private static string MethodText(TokenPaymentMethod method, string language)
    {
        var es = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return method switch
        {
            TokenPaymentMethod.CommissionBalance => es ? "saldo de comisiones" : "commission balance",
            TokenPaymentMethod.EWallet           => "eWallet",
            TokenPaymentMethod.CreditCard        => es ? "tarjeta" : "card",
            TokenPaymentMethod.Crypto            => es ? "cripto" : "crypto",
            _                                    => method.ToString()
        };
    }
}
