using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// Precio de un token = suma de lo que el alta cobraría por cada producto otorgado (SetupFee +
/// MonthlyFee), una vez cada uno: QuantityGranted no multiplica porque al canjear se entrega uno.
/// Se vende si el tipo está marcado como vendible, tiene al menos un producto otorgado, todos
/// activos, y el total es mayor que cero.
/// </summary>
public class TokenPricingServiceTests
{
    private static readonly DateTime Seed = new(2026, 10, 1);

    private static Product Producto(string id, decimal setup, decimal monthly, bool activo = true) => new()
    {
        Id = id, Name = id, Description = id, ImageUrl = "", SetupFee = setup, MonthlyFee = monthly,
        IsActive = activo, CreatedBy = "seed", CreationDate = Seed
    };

    private static TokenType Tipo(int id, string nombre, bool vendible = true, bool activo = true,
        TokenCategory cat = TokenCategory.Enrollment) => new()
    {
        Id = id, Name = nombre, IsActive = activo, IsPurchasable = vendible, Category = cat,
        CreatedBy = "seed", CreationDate = Seed
    };

    private static TokenTypeProduct Vinculo(int tipo, string producto,
        TokenProductRole rol = TokenProductRole.Granted, int cantidad = 1) => new()
    {
        TokenTypeId = tipo, ProductId = producto, Role = rol, QuantityGranted = cantidad,
        CreatedBy = "seed", CreationDate = Seed
    };

    private static AppDbContext Catalogo()
    {
        var db = TestDbContextFactory.Create();
        db.Products.AddRange(
            Producto("prod-vip", 50m, 49.99m),
            Producto("prod-sub", 0m, 40m),
            Producto("prod-off", 10m, 10m, activo: false),
            Producto("prod-free", 0m, 0m),
            Producto("prod-guest", 0m, 10m));
        db.TokenTypes.AddRange(
            Tipo(13, "Enrollment: VIP Member"),
            Tipo(64, "Enrollment: Ambassador + VIP"),
            Tipo(86, "Enrollment: Ambassador + VIP FREE", vendible: false),
            Tipo(40, "Dos VIP"),
            Tipo(56, "Upgrade: Guest to VIP", cat: TokenCategory.Upgrade),
            Tipo(70, "Producto inactivo"),
            Tipo(71, "Sin precio"),
            Tipo(99, "Tipo inactivo", activo: false));
        db.TokenTypeProducts.AddRange(
            Vinculo(13, "prod-vip"),
            Vinculo(64, "prod-sub"), Vinculo(64, "prod-vip"),
            Vinculo(86, "prod-sub"), Vinculo(86, "prod-vip"),
            Vinculo(40, "prod-vip", cantidad: 2),
            Vinculo(56, "prod-guest", TokenProductRole.UpgradeFrom), Vinculo(56, "prod-vip", TokenProductRole.UpgradeTo),
            Vinculo(70, "prod-vip"), Vinculo(70, "prod-off"),
            Vinculo(71, "prod-free"),
            Vinculo(99, "prod-vip"));
        db.SaveChanges();
        return db;
    }

    [Theory]
    [InlineData(13, 99.99)]   // un producto
    [InlineData(64, 139.99)]  // Ambassador (0 + 40) + VIP (50 + 49.99)
    [InlineData(40, 99.99)]   // VIP con QuantityGranted 2: al canjear se entrega uno, se cobra uno
    public async Task El_precio_es_la_suma_de_los_productos_otorgados(int tipo, double precio)
    {
        var quote = await new TokenPricingService(Catalogo()).QuoteAsync(tipo);

        quote.IsSuccess.Should().BeTrue();
        quote.Value!.UnitPrice.Should().Be((decimal)precio);
    }

    [Fact]
    public async Task Guarda_los_productos_de_los_que_salio_el_precio()
        => (await new TokenPricingService(Catalogo()).QuoteAsync(64)).Value!.ProductIds
            .Should().BeEquivalentTo("prod-sub", "prod-vip");

    [Theory]
    [InlineData(86,    TokenPurchaseErrors.NotForSale)]
    [InlineData(56,    TokenPurchaseErrors.NoGrantedProduct)]
    [InlineData(70,    TokenPurchaseErrors.ProductInactive)]
    [InlineData(71,    TokenPurchaseErrors.PriceNotSet)]
    [InlineData(99,    TokenPurchaseErrors.TokenTypeNotFound)]
    [InlineData(12345, TokenPurchaseErrors.TokenTypeNotFound)]
    public async Task Lo_que_no_se_vende_lo_dice_con_su_codigo(int tipo, string codigo)
    {
        var quote = await new TokenPricingService(Catalogo()).QuoteAsync(tipo);

        quote.IsSuccess.Should().BeFalse();
        quote.ErrorCode.Should().Be(codigo);
    }

    [Fact]
    public async Task El_catalogo_lista_los_activos_y_marca_los_que_no_se_venden()
    {
        var items = await new TokenPricingService(Catalogo()).GetCatalogAsync();

        items.Should().NotContain(i => i.TokenTypeId == 99);
        items.Single(i => i.TokenTypeId == 64).Purchasable.Should().BeTrue();
        var free = items.Single(i => i.TokenTypeId == 86);
        free.Purchasable.Should().BeFalse();
        free.NotPurchasableReason.Should().Be(TokenPurchaseErrors.NotForSale);
    }
}
