using Microsoft.EntityFrameworkCore;
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

/// <summary>
/// Contra SQL Server real (solo lectura): "contiene" se traduce a UPPER + intercalación binaria y sigue sin distinguir
/// mayúsculas; con menos de 3 caracteres pasa a "empieza por", que usa el índice.
/// </summary>
public class ContieneBinarioEnSqlServerTests
{
    private static List<GridFilter> Filtro(string op, string valor) =>
        new() { new GridFilter { Field = nameof(MemberProfile.Email), Operator = op, Value = valor } };

    [SqlServerFact]
    public async Task Contiene_compara_en_binario_y_encuentra_sin_importar_mayusculas()
    {
        await using var db = SqlServerTestDb.Db();
        var correo = await db.MemberProfiles.AsNoTracking().Where(m => m.Email.Length > 8)
            .Select(m => m.Email).FirstOrDefaultAsync();
        if (correo is null) return; // base sin miembros: nada que comprobar

        var trozo = correo.Substring(2, 5).ToUpperInvariant();
        var consulta = db.MemberProfiles.AsNoTracking().ApplyFilters(Filtro("contains", trozo), baseSinMayusculas: true);

        consulta.ToQueryString().Should().Contain("COLLATE Latin1_General_100_BIN2").And.Contain("UPPER(");
        (await consulta.Select(m => m.Email).ToListAsync()).Should().Contain(correo);
    }

    [SqlServerFact]
    public void Contiene_con_menos_de_tres_caracteres_pasa_a_empieza_por()
    {
        using var db = SqlServerTestDb.Db();

        var sql = db.MemberProfiles.AsNoTracking().ApplyFilters(Filtro("contains", "ab"), baseSinMayusculas: true)
            .ToQueryString();

        sql.Should().NotContain("BIN2").And.NotContain("LIKE N'%");
    }
}
