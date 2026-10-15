using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>Solo se ofrecen las monedas candidatas con dirección no vacía.</summary>
public class CryptoDepositAddressReaderTests
{
    [Fact]
    public async Task Devuelve_las_monedas_con_direccion_y_su_red()
    {
        var db = TestDbContextFactory.Create();
        GlobalParameter P(string k, string v) => new() { Key = k, Value = v, CreatedBy = "seed", CreationDate = DateTime.Now };
        db.GlobalParameters.AddRange(
            P("Crypto.DepositAddress.BTC", " bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh "),
            P("Crypto.DepositAddress.ETH", ""),
            P("Crypto.DepositAddress.USDT", "TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5"),
            P("Crypto.DepositNetwork.USDT", "TRC-20"),
            P("Crypto.DepositAddress.DOGE", "DH5yaieqoZN36fDVciNyRueRGvGLR3mr7L"));
        await db.SaveChangesAsync();

        var list = await new CryptoDepositAddressReader(db).GetConfiguredAsync();

        list.Select(d => d.Currency).Should().Equal("BTC", "USDT");
        list[0].Address.Should().Be("bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh");
        list[0].Network.Should().BeNull();
        list[1].Network.Should().Be("TRC-20");
    }

    [Fact]
    public async Task Ignora_las_direcciones_en_blanco_y_recorta_la_red()
    {
        var db = TestDbContextFactory.Create();
        GlobalParameter P(string k, string v) => new() { Key = k, Value = v, CreatedBy = "seed", CreationDate = DateTime.Now };
        db.GlobalParameters.AddRange(
            P("Crypto.DepositAddress.BTC", "   "),
            P("Crypto.DepositNetwork.BTC", "Bitcoin"),
            P("Crypto.DepositAddress.ETH", "0x52908400098527886E0F7030069857D2E4169EE7"),
            P("Crypto.DepositNetwork.ETH", "  ERC-20  "),
            P("Crypto.DepositAddress.USDT", "TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5"),
            P("Crypto.DepositNetwork.USDT", "   "));
        await db.SaveChangesAsync();

        var list = await new CryptoDepositAddressReader(db).GetConfiguredAsync();

        list.Select(d => d.Currency).Should().Equal("ETH", "USDT");
        list[0].Network.Should().Be("ERC-20");
        list[1].Network.Should().BeNull("una red en blanco no es una red.");
    }

    [Fact]
    public async Task El_orden_es_el_de_las_monedas_candidatas_y_no_el_de_la_base()
    {
        var db = TestDbContextFactory.Create();
        GlobalParameter P(string k, string v) => new() { Key = k, Value = v, CreatedBy = "seed", CreationDate = DateTime.Now };
        db.GlobalParameters.AddRange(
            P("Crypto.DepositAddress.USDT", "TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5"),
            P("Crypto.DepositAddress.ETH", "0x52908400098527886E0F7030069857D2E4169EE7"),
            P("Crypto.DepositAddress.BTC", "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"));
        await db.SaveChangesAsync();

        var list = await new CryptoDepositAddressReader(db).GetConfiguredAsync();

        list.Select(d => d.Currency).Should().Equal("BTC", "ETH", "USDT");
    }

    [Fact]
    public async Task Sin_parametros_no_ofrece_ninguna_moneda()
        => (await new CryptoDepositAddressReader(TestDbContextFactory.Create()).GetConfiguredAsync())
            .Should().BeEmpty();
}
