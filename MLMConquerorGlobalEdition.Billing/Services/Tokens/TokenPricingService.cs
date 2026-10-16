using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>Un tipo de token con su precio de venta, o con la razón por la que no se vende.</summary>
public sealed record TokenQuote(
    int                   TokenTypeId,
    string                Name,
    string?               Description,
    bool                  IsGuestPass,
    IReadOnlyList<string> ProductIds,
    decimal?              UnitPrice,
    bool                  Purchasable,
    string?               NotPurchasableReason);

public interface ITokenPricingService
{
    /// <summary>Todos los tipos activos, vendibles o no, para que la pantalla sepa qué ofrecer.</summary>
    Task<IReadOnlyList<TokenQuote>> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>El precio de un tipo, o un fallo con el código de por qué no se vende.</summary>
    Task<Result<TokenQuote>> QuoteAsync(int tokenTypeId, CancellationToken ct = default);
}

/// <inheritdoc cref="ITokenPricingService"/>
public sealed class TokenPricingService : ITokenPricingService
{
    private readonly AppDbContext _db;

    public TokenPricingService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TokenQuote>> GetCatalogAsync(CancellationToken ct = default)
    {
        var types = await _db.TokenTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var typeIds = types.Select(t => t.Id).ToList();
        var links = await _db.TokenTypeProducts.AsNoTracking()
            .Where(l => typeIds.Contains(l.TokenTypeId) && l.Role == TokenProductRole.Granted)
            .ToListAsync(ct);

        var products = await ProductsAsync(links, ct);

        return types
            .Select(t => Price(t, links.Where(l => l.TokenTypeId == t.Id).ToList(), products))
            .ToList();
    }

    public async Task<Result<TokenQuote>> QuoteAsync(int tokenTypeId, CancellationToken ct = default)
    {
        var type = await _db.TokenTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tokenTypeId && t.IsActive, ct);

        if (type is null)
            return Result<TokenQuote>.Failure(TokenPurchaseErrors.TokenTypeNotFound, "Token type not found or inactive.");

        var links = await _db.TokenTypeProducts.AsNoTracking()
            .Where(l => l.TokenTypeId == tokenTypeId && l.Role == TokenProductRole.Granted)
            .ToListAsync(ct);

        var quote = Price(type, links, await ProductsAsync(links, ct));

        return quote.Purchasable
            ? Result<TokenQuote>.Success(quote)
            : Result<TokenQuote>.Failure(quote.NotPurchasableReason!,
                $"Token type '{type.Name}' cannot be purchased ({quote.NotPurchasableReason}).");
    }

    private async Task<Dictionary<string, Product>> ProductsAsync(
        IReadOnlyCollection<TokenTypeProduct> links, CancellationToken ct)
    {
        var ids = links.Select(l => l.ProductId).Distinct().ToList();
        return await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    /// <summary>
    /// Suma de lo que el alta cobraría por cada producto otorgado (SetupFee + MonthlyFee, como
    /// SelectProductsHandler). Sin precios promo: el token sustituye al cobro normal. NO se multiplica
    /// por QuantityGranted: al canjear, el token entrega un producto de cada uno (QuantityGranted no se
    /// usa al canjear), así que cobrar por cantidad sería cobrar lo que no se entrega.
    /// IsPurchasable va primero: las variantes FREE otorgan los mismos productos que las de pago, así
    /// que sin el flag se venderían a precio completo.
    /// </summary>
    private static TokenQuote Price(
        TokenType type, IReadOnlyCollection<TokenTypeProduct> granted, IReadOnlyDictionary<string, Product> products)
    {
        TokenQuote NotSold(string reason) =>
            new(type.Id, type.Name, type.Description, type.IsGuestPass, Array.Empty<string>(), null, false, reason);

        if (!type.IsPurchasable) return NotSold(TokenPurchaseErrors.NotForSale);
        if (granted.Count == 0)  return NotSold(TokenPurchaseErrors.NoGrantedProduct);

        var total = 0m;
        foreach (var link in granted)
        {
            if (!products.TryGetValue(link.ProductId, out var product) || !product.IsActive)
                return NotSold(TokenPurchaseErrors.ProductInactive);

            total += product.SetupFee + product.MonthlyFee;
        }

        if (total <= 0) return NotSold(TokenPurchaseErrors.PriceNotSet);

        var ids = granted.Select(g => g.ProductId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new TokenQuote(type.Id, type.Name, type.Description, type.IsGuestPass, ids, total, true, null);
    }
}
