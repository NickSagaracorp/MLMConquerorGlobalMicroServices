using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M (R12): las consultas que deben hacer seek, con el SQL que genera EF de verdad y como lo manda
/// EF (sp_executesql con parámetros). Con una variable local el optimizador elige otro plan: con
/// parámetro, IsDescendantOf(@p) = CAST(1 AS bit) recorría el índice entero (desviación de la Tarea 13).
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPlanesDeEjecucionTests
{
    [SqlServerFact]
    public async Task El_equipo_por_Node_hace_seek_en_el_indice_en_profundidad_y_no_recorre_el_arbol()
    {
        await using var db = SqlServerReportes.Db();
        var raiz = HierarchyId.Parse("/1/");

        var plan = await PlanDeEjecucion.CompiladoAsync(db, db.GenealogyTree.AsNoTracking().CurrentUnder(raiz).Select(g => g.MemberId));

        plan.Should().Contain("IX_GenealogyTree_Node").And.Contain(PlanDeEjecucion.Seek);
        PlanDeEjecucion.Recorridos(plan, "GenealogyTree", "[g]").Should().BeEmpty();
    }

    [SqlServerFact]
    public async Task El_equipo_unido_a_los_perfiles_hace_seek_y_no_recorre_el_arbol()
    {
        await using var db = SqlServerReportes.Db();
        var raiz = HierarchyId.Parse("/1/");

        var plan = await PlanDeEjecucion.CompiladoAsync(db,
            from g in db.GenealogyTree.AsNoTracking().CurrentUnder(raiz)
            join m in db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            where m.MemberType == MemberType.ExternalMember
            select new { m.MemberId, g.Level });

        plan.Should().Contain("IX_GenealogyTree_Node").And.Contain(PlanDeEjecucion.Seek);
        PlanDeEjecucion.Recorridos(plan, "GenealogyTree", "[g]").Should().BeEmpty();
    }

    [SqlServerFact]
    public async Task Todos_los_nodos_bajo_un_Node_hacen_seek()
    {
        await using var db = SqlServerReportes.Db();

        var plan = await PlanDeEjecucion.CompiladoAsync(db, db.GenealogyTree.AsNoTracking().Under(HierarchyId.Parse("/1/2/")).Select(g => g.MemberId));

        PlanDeEjecucion.Recorridos(plan, "GenealogyTree").Should().BeEmpty();
        plan.Should().Contain("IX_GenealogyTree_Node").And.Contain(PlanDeEjecucion.Seek);
        // Con IsDescendantOf en el filtro, SQL Server estima 1 fila para cualquier subárbol y elige bucles con
        // búsquedas por clave: con el equipo de 119.400 personas, 9 s frente a 46 ms con el rango solo.
        plan.Should().NotContain("IsDescendantOf", "el rango basta: los ordinales son enteros (GenealogyNodes)");
    }

    [SqlServerFact]
    public async Task La_ultima_raiz_hace_seek_en_el_indice_en_anchura()
    {
        await using var db = SqlServerReportes.Db();

        var plan = await PlanDeEjecucion.CompiladoAsync(db, db.GenealogyTree.AsNoTracking().IgnoreQueryFilters()
            .Where(g => g.Node != null && g.Node.GetLevel() == 1)
            .OrderByDescending(g => g.Node).Select(g => g.Node).Take(1));

        plan.Should().Contain("IX_GenealogyTree_NodeLevel_Node").And.Contain(PlanDeEjecucion.Seek);
    }
}
