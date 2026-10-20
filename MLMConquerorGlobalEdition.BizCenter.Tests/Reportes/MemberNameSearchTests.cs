using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ8): la caida InMemory de MemberNameSearch (StartsWith, sin ignorar tildes: nombres sin tildes).</summary>
public class MemberNameSearchTests
{
    [Fact]
    public async Task En_memoria_busca_por_el_comienzo_del_nombre_completo()
    {
        await using var db = ReportesDb.Nueva();
        foreach (var (id, nombre, apellido) in new[] { ("AMB-1", "Lucia", "Diaz"), ("AMB-2", "Luciano", "Perez"), ("AMB-3", "Ana Lucia", "Gomez") })
        {
            var perfil = ReportesDb.Miembro(db, id);
            perfil.FirstName = nombre;
            perfil.LastName = apellido;
        }
        await db.SaveChangesAsync();

        string[] Buscar(string termino) =>
            MemberNameSearch.ApplyInMemory(db.MemberProfiles, termino).Select(m => m.MemberId).AsEnumerable().Order().ToArray();

        Buscar("lucia diaz").Should().Equal("AMB-1");
        Buscar("DIAZ").Should().Equal("AMB-1");
        Buscar("luci").Should().Equal("AMB-1", "AMB-2");
        Buscar("ucia").Should().BeEmpty();
        Buscar("amb-3").Should().Equal("AMB-3");
        Buscar("amb-").Should().BeEmpty("hasta el primer digito, el prefijo comun de todos los ids no busca por id");
    }

    [Theory]
    [InlineData("ana", "ana%")]
    [InlineData(" 50%_[x] ", "50[%][_][[]x]%")]
    public void El_patron_escapa_los_comodines_y_termina_en_porcentaje(string termino, string patron) =>
        MemberNameSearch.Pattern(termino).Should().Be(patron);

    /// <summary>Revision del bloque 5 (minor b): el termino se corta antes de anadir el '%', que nunca se pierde.</summary>
    [Fact]
    public void El_patron_corta_el_termino_y_conserva_el_porcentaje_final()
    {
        var patron = MemberNameSearch.Pattern(new string('a', 500));
        patron.Should().HaveLength(MemberNameSearch.MaxTermLength + 1).And.EndWith("%");
        MemberNameSearch.PatternParameter("@t", new string('%', 500)).Size.Should()
            .BeGreaterThanOrEqualTo(MemberNameSearch.MaxTermLength * 3 + 1, "cabe el termino mas largo con todo escapado");
    }

    /// <summary>Revision del bloque 5 (C1): solo se busca por id cuando el termino llega a los digitos.</summary>
    [Theory]
    [InlineData("a", false)]
    [InlineData("am", false)]
    [InlineData("amb", false)]
    [InlineData("AMB-", false)]
    [InlineData("mari", false)]
    [InlineData("000003", false)]
    [InlineData("amb-1", true)]
    [InlineData(" AMB-700829 ", true)]
    [InlineData("mbr-0", true)]
    [InlineData("root1", true)]
    public void Busca_por_id_solo_pasado_el_prefijo_comun(string termino, bool porId)
    {
        MemberNameSearch.SearchesId(termino).Should().Be(porId);
        MemberNameSearch.MatchingIdsSql(termino, 10).Sql.Contains("MemberId LIKE").Should().Be(porId);
    }

    [Fact]
    public void Cada_rama_lleva_su_tope_de_candidatos()
    {
        var (sql, parametros) = MemberNameSearch.MatchingIdsSql("amb-1", 1_000, "@x");
        System.Text.RegularExpressions.Regex.Matches(sql, @"SELECT TOP \(@x_top\)").Should().HaveCount(3);
        parametros.Single(p => p.ParameterName == "@x_top").Value.Should().Be(1_000);
        parametros.Single(p => p.ParameterName == "@x_t").Value.Should().Be("amb-1%");
    }
}
