using MLMConquerorGlobalEdition.Repository.Configurations;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

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

    /// <summary>
    /// Plan Q, Tarea 26: Mi equipo de inscripcion sin orden pedido pagina por Node (Tarea 22) sin la antijoin del nodo
    /// vigente (Tarea 21), y la pagina sale del recorrido de IX_GenealogyTree_Node sin ordenar el rango.
    /// </summary>
    [SqlServerFact]
    public async Task Mi_equipo_de_inscripcion_pagina_por_Node_sin_ordenar_el_rango()
    {
        const string raizMayor = "AMB-700829";
        var cap = new CapturadorDeSql();
        await using (var db = cap.Db())
        {
            var servicio = new EnrollmentTeamService(db, CalificadoEnLosEquiposTests.Rangos(db),
                new EnrollmentTeamPointsService(db), CalificadoEnLosEquiposTests.Calificacion(db));
            await servicio.GetMyTeamGridAsync(raizMayor, new GridDataRequest { Page = 1000, PageSize = 20 });
        }
        var delEquipo = cap.Textos.Where(t => t.Contains("[GenealogyTree]") && t.Contains("[MemberProfiles]")).ToList();
        delEquipo.Should().Contain(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"ORDER BY \[\w+\]\.\[Node\]"));
        delEquipo.Should().NotContain(t => t.Contains("NOT EXISTS"), "con un nodo vivo por miembro no hay antijoin (Tarea 21)");

        await using var db2 = SqlServerReportes.Db();
        var raiz = await db2.GenealogyTree.AsNoTracking().NodeOfAsync(raizMayor);
        // La forma del servicio (desviacion de la Tarea 26): la pagina sale del arbol y despues se unen los perfiles.
        var pagina =
            from g in db2.GenealogyTree.AsNoTracking().CurrentUnder(raiz).Where(g => g.MemberId != raizMayor)
                .OrderBy(g => g.Node).Skip(19_980).Take(20)
            join m in db2.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            orderby g.Node
            select new { m.MemberId, m.FirstName, m.LastName, g.Level };

        var plan = await PlanDeEjecucion.CompiladoAsync(db2, pagina);

        plan.Should().Contain("IX_GenealogyTree_Node").And.Contain(PlanDeEjecucion.Seek);
        PlanDeEjecucion.Recorridos(plan, "GenealogyTree").Should().BeEmpty();
        plan.Should().NotContain("PhysicalOp=\"Sort\"", "la pagina sale en el orden del indice");
    }

    /// <summary>
    /// Plan Q (Tarea 28): Mi equipo binario sin orden pedido toma la pagina de la clave (UX_DualTeamTree_BinaryPath,
    /// cubriente para MemberId) antes de unir los perfiles; con el OFFSET sobre la union se ordenaba el rango entero.
    /// </summary>
    [SqlServerFact]
    public async Task Mi_equipo_binario_toma_la_pagina_de_la_clave_antes_de_unir_los_perfiles()
    {
        const string raizMayor = "AMB-700829";
        var cap = new CapturadorDeSql();
        await using (var db = cap.Db())
        {
            var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));
            var r = await servicio.GetMyTeamGridAsync(raizMayor, new GridDataRequest { Page = 1000, PageSize = 20 });
            r.Items.Should().HaveCount(20);
        }
        var pagina = cap.Textos.Single(t => t.Contains("FETCH NEXT") && t.Contains("[MemberProfiles]"));
        pagina.IndexOf("FETCH NEXT", StringComparison.Ordinal)
            .Should().BeLessThan(pagina.IndexOf("[MemberProfiles]", StringComparison.Ordinal), "la pagina sale de la clave");
    }

    /// <summary>
    /// Revision del bloque 5 (I1): el total de Mi equipo de inscripcion sin filtros es el numero de filas que la rejilla
    /// lista (los nodos vivos del equipo, tambien los pendientes), asi que la ultima pagina se alcanza y trae el resto.
    /// Con EnrollmentTeamSize (118.228 en AMB-700829, solo activados) quedaban fuera unas 1.170 filas. Se cuenta por el
    /// indice estrecho IX_GenealogyTree_Node_Vivo.
    /// </summary>
    [SqlServerFact]
    public async Task Mi_equipo_de_inscripcion_da_el_total_de_las_filas_y_alcanza_la_ultima_pagina()
    {
        const string raizMayor = "AMB-700829";
        var cap = new CapturadorDeSql();
        await using var db = cap.Db();
        var raiz = await db.GenealogyTree.AsNoTracking().NodeOfAsync(raizMayor);
        var filas = await (from g in db.GenealogyTree.AsNoTracking().CurrentUnder(raiz)
                           where g.MemberId != raizMayor
                           select g.MemberId).CountAsync();
        var servicio = new EnrollmentTeamService(db, CalificadoEnLosEquiposTests.Rangos(db),
            new EnrollmentTeamPointsService(db), CalificadoEnLosEquiposTests.Calificacion(db));
        cap.Limpiar();

        var primera = await servicio.GetMyTeamGridAsync(raizMayor, new GridDataRequest { PageSize = 20 });
        var paginas = (primera.TotalCount + 19) / 20;
        var ultima = await servicio.GetMyTeamGridAsync(raizMayor, new GridDataRequest { Page = paginas, PageSize = 20 });
        var despues = await servicio.GetMyTeamGridAsync(raizMayor, new GridDataRequest { Page = paginas + 1, PageSize = 20 });

        primera.TotalCount.Should().Be(filas);
        ultima.Items.Should().HaveCount(filas - (paginas - 1) * 20);
        despues.Items.Should().BeEmpty();

        var conteos = cap.Textos.Where(t => t.Contains("COUNT(")).ToList();
        conteos.Should().NotBeEmpty().And.OnlyContain(t => t.Contains($"INDEX({GenealogyConfiguration.LiveNodeIndexName})"),
            "el total cuenta el rango por el indice estrecho, no la union con los perfiles");
    }
}
