using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Repository.Services.Links;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class MyLinksServiceTests
{
    private static MyLinksOptions Opciones() => new()
    {
        Links =
        {
            new MyLinkTemplate { Key = "mwrlife", Label = "MWR Life",    Url = "https://mwrlife.com/{slug}" },
            new MyLinkTemplate { Key = "academy", Label = "MWR Academy", Url = "https://mwracademy.com" }
        }
    };

    [Fact]
    public async Task Con_slug_rellena_los_personales_y_da_los_corporativos()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001", slug: "ana");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000001");

        vista.SlugMissing.Should().BeFalse();
        vista.Personal.Should().ContainSingle().Which.Url.Should().Be("https://mwrlife.com/ana");
        vista.Corporate.Should().ContainSingle().Which.Url.Should().Be("https://mwracademy.com");
    }

    [Fact]
    public async Task Sin_slug_avisa_y_solo_da_los_corporativos()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000001");

        vista.SlugMissing.Should().BeTrue();
        vista.Personal.Should().BeEmpty();
        vista.Corporate.Should().HaveCount(1);
    }

    [Fact]
    public async Task El_slug_se_escapa()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001", slug: "ana lópez");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000001");

        vista.Personal.Single().Url.Should().Be("https://mwrlife.com/ana%20l%C3%B3pez");
    }

    [Fact]
    public async Task Usa_el_slug_del_miembro_pedido_y_no_el_de_otro()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-000001", slug: "ana");
        ReportesDb.Miembro(db, "AMB-000002", slug: "pedro");
        await db.SaveChangesAsync();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-000002");

        vista.Personal.Single().Url.Should().Be("https://mwrlife.com/pedro");
    }

    [Fact]
    public async Task Un_miembro_que_no_existe_es_como_sin_slug()
    {
        await using var db = ReportesDb.Nueva();

        var vista = await new MyLinksService(db, Opciones()).GetAsync("AMB-999999");

        vista.SlugMissing.Should().BeTrue();
        vista.Personal.Should().BeEmpty();
    }

    [Fact]
    public void El_registro_valida_antes_de_registrar()
    {
        var malas = new MyLinksOptions { Links = { new MyLinkTemplate { Key = "a", Label = "A", Url = "http://x.com" } } };

        var act = () => new ServiceCollection().AddMyLinks(malas);

        act.Should().Throw<InvalidOperationException>();
        new ServiceCollection().AddMyLinks(new MyLinksOptions())
            .Should().Contain(d => d.ServiceType == typeof(IMyLinksService));
    }
}
