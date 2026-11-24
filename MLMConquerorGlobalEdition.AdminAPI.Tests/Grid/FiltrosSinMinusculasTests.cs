using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Grid;

/// <summary>
/// En SQL Server, que ya compara sin mayúsculas, los filtros y la búsqueda de los grids van sin ToLower para que el
/// índice de la columna sirva; donde la comparación sí distingue (colecciones en memoria) siguen pasando a minúsculas.
/// </summary>
public class FiltrosSinMinusculasTests
{
    private static List<GridFilter> PorCorreo(string op, string valor) =>
        new() { new GridFilter { Field = nameof(MemberProfile.Email), Operator = op, Value = valor } };

    [Theory]
    [InlineData("equal")]
    [InlineData("startswith")]
    [InlineData("contains")]
    public void Con_la_base_sin_mayusculas_el_filtro_compara_la_columna_tal_cual(string op)
    {
        using var db = InMemoryDbHelper.Create();

        var consulta = db.MemberProfiles.ApplyFilters(PorCorreo(op, "ESC-1@Example.com"), baseSinMayusculas: true);

        consulta.Expression.ToString().Should().NotContain("ToLower");
    }

    [Fact]
    public void Con_la_base_sin_mayusculas_la_busqueda_compara_la_columna_tal_cual()
    {
        using var db = InMemoryDbHelper.Create();

        var consulta = db.MemberProfiles.ApplySearch("Garcia", new[] { nameof(MemberProfile.LastName) }, baseSinMayusculas: true);

        consulta.Expression.ToString().Should().NotContain("ToLower");
    }

    [Fact]
    public void Sin_ese_aviso_se_sigue_pasando_a_minusculas()
    {
        using var db = InMemoryDbHelper.Create();

        db.MemberProfiles.ApplyFilters(PorCorreo("equal", "x@y.com"), baseSinMayusculas: false)
            .Expression.ToString().Should().Contain("ToLower");
    }

    [Fact]
    public void En_una_coleccion_en_memoria_siempre_se_pasa_a_minusculas()
    {
        var lista = new List<MemberProfile> { new() { MemberId = "ESC-1", Email = "esc-1@example.com" } }.AsQueryable();

        lista.ApplyFilters(PorCorreo("equal", "ESC-1@EXAMPLE.COM"), baseSinMayusculas: true)
            .Should().ContainSingle();
    }
}
