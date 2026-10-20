using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Revision del bloque 2 (I-3): el vaciado por la union de caminos da exactamente lo mismo que aplicar cada delta, uno a
/// uno, a cada ascendiente por la clave (el recuento ingenuo, en C# con DualBinaryPath). Arbol al azar con una cadena de
/// mas de 1.000 niveles (claves de mas de 128 bytes: el prefijo comun cruza trozos de 64), ramas, una segunda raiz,
/// deltas de pierna y propios con signo, varios por arranque, arranques sin nodo, y tandas pequenas para forzar la mitad
/// de la ventana y varias marcas de agua. Raices y nodos propios, borrados por el prefijo al terminar.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerVaciadoEquivalenteTests
{
    private sealed class Piernas
    {
        public decimal LP, RP;
        public int LC, RC, LQ, RQ, SQ;
    }

    [SqlServerTheory]
    [InlineData(1019, 1000, 400_000)]
    [InlineData(7, 50, 600)]
    [InlineData(42, 7, 400_000)]
    [InlineData(2026, 1000, 1)]
    [InlineData(5, 1, 400_000)]
    public async Task El_vaciado_coincide_con_el_recuento_ingenuo(int semilla, int ventana, int maxFilas)
    {
        var rnd = new Random(semilla);
        var x = BinarioDb.Prefijo("PQU");
        // El arbol se confirma (cuelga de raices propias) y los deltas se encolan y se vacian en una transaccion que tiene la
        // cola en exclusivo (ningun otro vaciador, como los servicios locales, los toca) y que se CONFIRMA: deshacer cientos
        // de tandas con sus tablas temporales pasaba de los 15 minutos. Al final se borra todo por el prefijo.
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(900);
        try
        {
        var ahora = DateTime.Now;

        // Arbol: raiz A con una cadena honda y ramas; raiz B pequena.
        var nodos = new List<DualTeamEntity>();
        var libres = new List<(DualTeamEntity Padre, TreeSide Lado)>();
        DualTeamEntity Nuevo(DualTeamEntity e) { nodos.Add(e); db.DualTeamTree.Add(e); return e; }
        async Task<DualTeamEntity> Hijo(DualTeamEntity padre, TreeSide lado) =>
            Nuevo(await DualTeamNodes.ChildAsync(db, padre, $"{x}{nodos.Count:00000}", lado, ahora, "plan-q-prueba"));

        var a = Nuevo(await DualTeamNodes.RootAsync(db, $"{x}{nodos.Count:00000}", ahora, "plan-q-prueba"));
        var cadena = new List<DualTeamEntity> { a };
        var hondo = 1000 + rnd.Next(200);
        for (var i = 0; i < hondo; i++)
        {
            var lado = rnd.Next(2) == 0 ? TreeSide.Left : TreeSide.Right;
            var h = await Hijo(cadena[^1], lado);
            libres.Add((cadena[^1], lado == TreeSide.Left ? TreeSide.Right : TreeSide.Left));
            cadena.Add(h);
        }
        libres.Add((cadena[^1], TreeSide.Left));
        libres.Add((cadena[^1], TreeSide.Right));
        var b = Nuevo(await DualTeamNodes.RootAsync(db, $"{x}{nodos.Count:00000}", ahora, "plan-q-prueba"));
        libres.Add((b, TreeSide.Left));
        libres.Add((b, TreeSide.Right));
        for (var i = 0; i < 300; i++)
        {
            var j = rnd.Next(libres.Count);
            var (padre, lado) = libres[j];
            libres.RemoveAt(j);
            var h = await Hijo(padre, lado);
            libres.Add((h, TreeSide.Left));
            libres.Add((h, TreeSide.Right));
        }
        await db.SaveChangesAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        await DualTeamGate.EnterLegDrainAsync(db, lockTimeoutMs: 120_000);

        // Deltas: con signo, varios por arranque, de pierna y propios, y arranques sin nodo.
        var deltas = new List<DualLegDelta>();
        // Con ventanas de muy pocas filas o un tope de 1, cada delta es casi una tanda: menos deltas para no alargar la suite.
        var cuantos = ventana <= 7 || maxFilas < 1_000 ? 150 : 500;
        for (var i = 0; i < cuantos; i++)
        {
            string arranque;
            var r = rnd.NextDouble();
            if (r < 0.05) arranque = $"{x}SIN{rnd.Next(5)}";
            else if (r < 0.55) arranque = cadena[rnd.Next(cadena.Count)].MemberId;
            else arranque = nodos[rnd.Next(nodos.Count)].MemberId;
            var propio = rnd.NextDouble() < 0.25;
            deltas.Add(new DualLegDelta
            {
                StartMemberId = arranque,
                Side = propio ? TreeSide.Left : (rnd.Next(2) == 0 ? TreeSide.Left : TreeSide.Right),
                Points = propio && rnd.Next(2) == 0 ? 0 : Math.Round((decimal)(rnd.NextDouble() * 200 - 60), 2),
                NodeCount = propio ? 0 : rnd.Next(-2, 5),
                QualifiedCount = rnd.Next(-2, 4),
                OwnChange = propio,
                CreationDate = ahora, CreatedBy = "plan-q-prueba"
            });
        }
        db.DualLegDeltas.AddRange(deltas);
        await db.SaveChangesAsync();

        // Recuento ingenuo: cada delta, a cada ascendiente por la clave.
        var porClave = nodos.ToDictionary(n => Convert.ToHexString(n.BinaryPath!), n => n.MemberId);
        var porMiembro = nodos.ToDictionary(n => n.MemberId);
        var esperado = nodos.ToDictionary(n => n.MemberId, _ => new Piernas());
        void Sumar(Piernas p, TreeSide lado, decimal pts, int cnt, int cal)
        {
            if (lado == TreeSide.Left) { p.LP += pts; p.LC += cnt; p.LQ += cal; }
            else { p.RP += pts; p.RC += cnt; p.RQ += cal; }
        }
        foreach (var d in deltas)
        {
            if (!porMiembro.TryGetValue(d.StartMemberId, out var s)) continue;
            var k = s.BinaryPath!;
            var prof = DualBinaryPath.Depth(k);
            if (d.OwnChange) esperado[s.MemberId].SQ += d.QualifiedCount;
            else Sumar(esperado[s.MemberId], d.Side, d.Points, d.NodeCount, d.QualifiedCount);
            for (var j = 0; j < prof; j++)
                if (porClave.TryGetValue(Convert.ToHexString(DualBinaryPath.Ancestor(k, j)), out var anc))
                    Sumar(esperado[anc], DualBinaryPath.SideBelow(k, j), d.Points, d.NodeCount, d.QualifiedCount);
        }

        await LegPointMaintenance.DrainAsync(db, options: new LegDrainOptions { WindowRows = ventana, MaxPathRows = maxFilas, UpdateChunk = 997 });

        var reales = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).ToListAsync();
        reales.Should().HaveCount(nodos.Count);
        foreach (var n in reales)
        {
            var e = esperado[n.MemberId];
            (n.LeftLegPoints, n.RightLegPoints, n.LeftLegCount, n.RightLegCount, n.LeftLegQualified, n.RightLegQualified, n.SelfQualified)
                .Should().Be((e.LP, e.RP, e.LC, e.RC, e.LQ, e.RQ, e.SQ), $"semilla {semilla}, nodo {n.MemberId} a {n.Depth} niveles");
        }

        // Sin nodo: los de pierna, apartados con su motivo; los propios, esperando en la cola.
        var sinNodo = deltas.Where(d => !porMiembro.ContainsKey(d.StartMemberId)).ToList();
        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId.StartsWith(x)))
            .Should().Be(sinNodo.Count(d => d.OwnChange));
        (await db.DualLegDeltaDeadLetters.CountAsync(d => d.StartMemberId.StartsWith(x) && d.Reason == "sin-nodo"))
            .Should().Be(sinNodo.Count(d => !d.OwnChange));
        await tx.CommitAsync();
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }
}
