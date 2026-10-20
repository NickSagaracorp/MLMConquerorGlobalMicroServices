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
            MemberNameSearch.Apply(db, db.MemberProfiles, termino).Select(m => m.MemberId).AsEnumerable().Order().ToArray();

        Buscar("lucia diaz").Should().Equal("AMB-1");
        Buscar("DIAZ").Should().Equal("AMB-1");
        Buscar("luci").Should().Equal("AMB-1", "AMB-2");
        Buscar("ucia").Should().BeEmpty();
        Buscar("amb-3").Should().Equal("AMB-3");
    }

    [Theory]
    [InlineData("ana", "ana%")]
    [InlineData(" 50%_[x] ", "50[%][_][[]x]%")]
    public void El_patron_escapa_los_comodines_y_termina_en_porcentaje(string termino, string patron) =>
        MemberNameSearch.Pattern(termino).Should().Be(patron);
}
