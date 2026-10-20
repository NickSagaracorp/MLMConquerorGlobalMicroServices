using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ8): busqueda por el comienzo del nombre completo o del id, sin tildes ni mayusculas, con indice.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerBusquedaPorNombreTests
{
    [SqlServerFact]
    public async Task Busca_por_el_comienzo_del_nombre_o_del_apellido_sin_tildes()
    {
        var x = BinarioDb.Prefijo("PQB");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}1", $"{x}P", TreeSide.Left),
            ($"{x}2", $"{x}1", TreeSide.Left), ($"{x}3", $"{x}1", TreeSide.Right));
        ReportesDb.Miembro(db, $"{x}P");
        foreach (var (id, nombre, apellido) in new[] { ("1", "Lucía", "Díaz"), ("2", "Luciano", "Pérez"), ("3", "Ana Lucía", "Gómez") })
        {
            var perfil = ReportesDb.Miembro(db, $"{x}{id}", $"{x}P");
            perfil.FirstName = nombre;
            perfil.LastName = apellido;
        }
        await db.SaveChangesAsync();
        var raiz = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");

        async Task<string[]> Buscar(string termino) =>
            (await DualSubtree.SearchUnder(db, raiz, termino, MemberNameSearch.ViewerCandidatesPerBranch).Select(d => d.MemberId).ToListAsync()).Order().ToArray();

        (await Buscar("lucia diaz")).Should().Equal($"{x}1");
        (await Buscar("diaz")).Should().Equal($"{x}1");
        (await Buscar("luci")).Should().Equal($"{x}1", $"{x}2");
        (await Buscar("ucia")).Should().BeEmpty();
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task La_busqueda_parte_del_indice_de_nombre()
    {
        await using var db = SqlServerReportes.Db();
        var mayor = await db.DualTeamTree.AsNoTracking()
            .OrderByDescending(d => d.LeftLegCount + d.RightLegCount).ThenBy(d => d.MemberId).FirstAsync();

        var plan = await PlanDeEjecucion.CompiladoAsync(db, DualSubtree.SearchUnder(db, mayor, "lucia", MemberNameSearch.ViewerCandidatesPerBranch).Select(d => d.MemberId));

        plan.Should().Contain("IX_MemberProfiles_SearchName").And.Contain(PlanDeEjecucion.Seek);
        PlanDeEjecucion.Recorridos(plan, "MemberProfiles").Should().BeEmpty();
        PlanDeEjecucion.Recorridos(plan, "DualTeamTree").Should().BeEmpty();
    }

    /// <summary>
    /// Revision del bloque 5 (I2): Mi equipo de inscripcion limitado busca con el mismo componente que el binario: parte de
    /// los indices de nombre y no recorre ni los perfiles ni el arbol (antes, un OR de tres LIKE sobre los perfiles unido
    /// al rango).
    /// </summary>
    [SqlServerFact]
    public async Task La_busqueda_de_inscripcion_parte_del_indice_de_nombre()
    {
        await using var db = SqlServerReportes.Db();
        var raiz = await db.GenealogyTree.AsNoTracking().NodeOfAsync("AMB-700829");

        var plan = await PlanDeEjecucion.CompiladoAsync(db,
            db.SearchUnder(raiz, "lucia", MemberNameSearch.GridCandidatesPerBranch).AsNoTracking().Select(g => g.MemberId));

        plan.Should().Contain("IX_MemberProfiles_SearchName").And.Contain("IX_MemberProfiles_SearchNameReversed")
            .And.Contain(PlanDeEjecucion.Seek);
        plan.Should().NotContain("[MemberProfiles].[IX_MemberProfiles_MemberId]", "«lucia» no llega a los digitos de un id");
        PlanDeEjecucion.Recorridos(plan, "MemberProfiles").Should().BeEmpty();
        PlanDeEjecucion.Recorridos(plan, "GenealogyTree").Should().BeEmpty();
    }

    /// <summary>Revision del bloque 5 (I2): la busqueda de inscripcion casa lo mismo que la del binario.</summary>
    [SqlServerFact]
    public async Task La_busqueda_de_inscripcion_casa_por_el_comienzo_del_nombre_o_del_id()
    {
        var x = "AMB-9" + Random.Shared.NextInt64(100_000_000, 999_999_999);
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        ReportesDb.Miembro(db, $"{x}0");
        ReportesDb.Nodo(db, $"{x}0", null, ReportesDb.Ruta($"{x}0"), 1);
        await db.SaveChangesAsync();
        foreach (var (id, nombre, apellido) in new[] { ("1", "Lucía", "Díaz"), ("2", "Luciano", "Pérez"), ("3", "Ana Lucía", "Gómez") })
        {
            var perfil = ReportesDb.Miembro(db, $"{x}{id}", $"{x}0");
            perfil.FirstName = nombre;
            perfil.LastName = apellido;
            ReportesDb.Nodo(db, $"{x}{id}", $"{x}0", ReportesDb.Ruta($"{x}0", $"{x}{id}"), 2);
        }
        await db.SaveChangesAsync();
        var raiz = await db.GenealogyTree.AsNoTracking().NodeOfAsync($"{x}0");

        async Task<string[]> Buscar(string termino) =>
            (await db.SearchUnder(raiz, termino, MemberNameSearch.GridCandidatesPerBranch).Select(g => g.MemberId).ToListAsync())
            .Order().ToArray();

        (await Buscar("lucia diaz")).Should().Equal($"{x}1");
        (await Buscar("diaz")).Should().Equal($"{x}1");
        (await Buscar("luci")).Should().Equal($"{x}1", $"{x}2");
        (await Buscar("ucia")).Should().BeEmpty();
        (await Buscar(x.ToLowerInvariant() + "3")).Should().Equal($"{x}3");
        (await Buscar(x)).Should().Equal($"{x}0", $"{x}1", $"{x}2", $"{x}3");
        await tx.RollbackAsync();
    }
}
