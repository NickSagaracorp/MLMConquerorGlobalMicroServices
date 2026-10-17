using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Interceptors;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M (desviación de la Tarea 13): el mismo SQL del equipo sirve a un equipo de 30 personas y a
/// uno de 119.400. Con el plan en caché del primero, el segundo tardaba 3–6 s; compilado para él,
/// 73 ms. Las consultas que pasan por GenealogySubtree.Under llevan una etiqueta y
/// <see cref="RecompileTaggedQueriesInterceptor"/> les añade OPTION (RECOMPILE).
/// </summary>
public class RecompilarElEquipoTests
{
    [Fact]
    public void Las_consultas_del_equipo_llevan_la_etiqueta()
    {
        using var db = SqlServerReportes.Db(); // solo construye el SQL, no abre conexión
        var sql = db.GenealogyTree.Under(HierarchyId.Parse("/1/")).Select(g => g.MemberId).ToQueryString();

        sql.Should().Contain(GenealogySubtree.RecompileTag);
    }

    [Fact]
    public void Con_la_etiqueta_se_anade_OPTION_RECOMPILE_al_final()
        => RecompileTaggedQueriesInterceptor.Apply($"-- {GenealogySubtree.RecompileTag}\n\nSELECT 1\nFROM [X]")
            .Should().EndWith("\nOPTION (RECOMPILE)");

    [Fact]
    public void Sin_la_etiqueta_no_se_toca_y_no_se_anade_dos_veces()
    {
        RecompileTaggedQueriesInterceptor.Apply("SELECT 1").Should().Be("SELECT 1");

        var una = RecompileTaggedQueriesInterceptor.Apply($"-- {GenealogySubtree.RecompileTag}\n\nSELECT 1");
        RecompileTaggedQueriesInterceptor.Apply(una).Should().Be(una);
    }

    [Fact]
    public void El_contexto_registra_el_interceptor()
    {
        using var db = SqlServerReportes.Db();
        db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors
            .Should().ContainSingle(i => i is RecompileTaggedQueriesInterceptor);
    }
}
