using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (R12): las consultas que deben hacer seek, con el SQL que genera EF de verdad.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPlanesDeEjecucionTests
{
    [SqlServerFact]
    public async Task El_equipo_por_Node_hace_seek_en_el_indice_en_profundidad()
    {
        await using var db = SqlServerReportes.Db();
        var raiz = HierarchyId.Parse("/1/");

        var plan = await PlanDeEjecucion.EstimadoAsync(
            db.GenealogyTree.AsNoTracking().CurrentUnder(raiz).Select(g => g.MemberId).ToQueryString());

        plan.Should().Contain("IX_GenealogyTree_Node").And.Contain(PlanDeEjecucion.Seek);
    }

    [SqlServerFact]
    public async Task La_ultima_raiz_hace_seek_en_el_indice_en_anchura()
    {
        await using var db = SqlServerReportes.Db();

        var plan = await PlanDeEjecucion.EstimadoAsync(db.GenealogyTree.AsNoTracking().IgnoreQueryFilters()
            .Where(g => g.Node != null && g.Node.GetLevel() == 1)
            .OrderByDescending(g => g.Node).Select(g => g.Node).Take(1).ToQueryString());

        plan.Should().Contain("IX_GenealogyTree_NodeLevel_Node").And.Contain(PlanDeEjecucion.Seek);
    }
}
