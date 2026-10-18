using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.Domain.Tests;

/// <summary>
/// Plan P, Tarea 9: la clave compacta del binario. R (4 bytes) + 1 bit por nivel + terminador. El orden de
/// SQL Server (ceros finales = relleno) es el inorden, y el subarbol de X es el rango exclusivo (Lo, Hi).
/// </summary>
public class DualBinaryPathTests
{
    private static string Hex(byte[] k) => Convert.ToHexString(k);

    private static byte[] Camino(params TreeSide[] lados)
    {
        var k = DualBinaryPath.Root(1);
        foreach (var l in lados) k = DualBinaryPath.Child(k, l);
        return k;
    }

    private const TreeSide L = TreeSide.Left, R = TreeSide.Right;

    [Fact]
    public void Los_ejemplos_de_la_tabla_byte_a_byte()
    {
        Hex(Camino()).Should().Be("0000000180");
        Hex(Camino(L)).Should().Be("0000000140");
        Hex(Camino(R)).Should().Be("00000001C0");
        Hex(Camino(L, L)).Should().Be("0000000120");
        Hex(Camino(L, R)).Should().Be("0000000160");
        Hex(Camino(R, L)).Should().Be("00000001A0");
        Hex(Camino(R, R)).Should().Be("00000001E0");
        Hex(Camino(L, L, L, L, L, L, L, R)).Should().Be("000000010180");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(8223)] [InlineData(13567)]
    public void La_profundidad_es_la_posicion_del_terminador(int k)
    {
        var key = DualBinaryPath.Root(42);
        for (var i = 0; i < k; i++) key = DualBinaryPath.Child(key, i % 3 == 0 ? R : L);
        DualBinaryPath.Depth(key).Should().Be(k);
        DualBinaryPath.RootOrdinal(key).Should().Be(42);
        key.Length.Should().Be(DualBinaryPath.RootBytes + k / 8 + 1);
    }

    [Fact]
    public void Con_8223_niveles_la_clave_cabe_en_la_clave_de_indice()
    {
        // Decision de Nick (2026-10-18): la reparacion deja 8.223 niveles en local.
        var key = DualBinaryPath.Root(int.MaxValue);
        for (var i = 0; i < 8223; i++) key = DualBinaryPath.Child(key, R);
        key.Length.Should().Be(1032).And.BeLessThan(DualBinaryPath.MaxBytes);
    }

    [Fact]
    public void Pasar_del_limite_lanza()
    {
        var key = DualBinaryPath.Root(1);
        for (var i = 0; i < DualBinaryPath.MaxDepth; i++) key = DualBinaryPath.Child(key, L);
        key.Length.Should().Be(DualBinaryPath.MaxBytes);
        var act = () => DualBinaryPath.Child(key, L);
        act.Should().Throw<DualTreeTooDeepException>();
    }

    [Fact]
    public void Una_clave_mal_formada_se_rechaza()
    {
        var act = () => DualBinaryPath.Depth(new byte[] { 0, 0, 0, 1, 0x00 });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void El_borde_derecho_acaba_en_la_raiz_siguiente()
    {
        var (_, hi) = DualBinaryPath.SubtreeBounds(Camino(R, R, R));
        Hex(hi).Should().StartWith("00000002").And.Subject.TrimEnd('0').Should().Be("00000002");
        var (_, hiRaiz) = DualBinaryPath.SubtreeBounds(DualBinaryPath.Root(1));
        DualBinaryPath.SqlCompare(hiRaiz, DualBinaryPath.Root(2)).Should().BeLessThan(0);
        DualBinaryPath.SqlCompare(hiRaiz, new byte[] { 0, 0, 0, 2 }).Should().Be(0);
    }

    [Fact]
    public void La_comparacion_ignora_los_ceros_finales_como_SQL_Server()
    {
        DualBinaryPath.SqlCompare(new byte[] { 0x80 }, new byte[] { 0x80, 0x00 }).Should().Be(0);
        DualBinaryPath.SqlCompare(new byte[] { 0x80 }, new byte[] { 0x80, 0x01 }).Should().BeLessThan(0);
    }

    /// <summary>
    /// Propiedad: en arboles aleatorios (cadenas largas y ramas balanceadas), para todo nodo X el rango
    /// (Lo, Hi) contiene exactamente su subarbol, las piernas exactamente los subarboles de sus hijos, y no
    /// hay dos claves iguales con la comparacion de SQL Server. Varios arboles conviven (ordinales distintos).
    /// </summary>
    [Fact]
    public void Propiedad_el_rango_es_exactamente_el_subarbol()
    {
        var azar = new Random(20261018);
        for (var arbol = 0; arbol < 50; arbol++)
        {
            var nodos = new List<(int Id, int? Padre, TreeSide Lado, byte[] Clave)>();
            var otraRaiz = DualBinaryPath.Root(arbol + 2);   // un arbol vecino que no debe colarse
            nodos.Add((0, null, L, DualBinaryPath.Root(arbol + 1)));
            nodos.Add((-1, null, L, otraRaiz));
            var libres = new List<(int Padre, TreeSide Lado)> { (0, L), (0, R) };
            var siguiente = 1;
            while (siguiente < 300)
            {
                // Mitad de las veces se alarga la ultima cadena (hasta 200 niveles); si no, un hueco al azar.
                var i = azar.Next(2) == 0 ? libres.Count - 1 : azar.Next(libres.Count);
                var (padre, lado) = libres[i];
                libres.RemoveAt(i);
                var clave = DualBinaryPath.Child(nodos.First(n => n.Id == padre).Clave, lado);
                if (DualBinaryPath.Depth(clave) > 200) continue;
                nodos.Add((siguiente, padre, lado, clave));
                libres.Add((siguiente, L));
                libres.Add((siguiente, R));
                siguiente++;
            }

            var hijos = nodos.Where(n => n.Padre is not null).ToLookup(n => n.Padre!.Value);
            HashSet<int> Subarbol(int id)
            {
                var s = new HashSet<int> { id };
                var pila = new Stack<int>(new[] { id });
                while (pila.Count > 0)
                    foreach (var h in hijos[pila.Pop()]) { s.Add(h.Id); pila.Push(h.Id); }
                return s;
            }
            HashSet<int> EnRango(byte[] lo, byte[] hi) => nodos
                .Where(n => DualBinaryPath.SqlCompare(n.Clave, lo) > 0 && DualBinaryPath.SqlCompare(n.Clave, hi) < 0)
                .Select(n => n.Id).ToHashSet();

            foreach (var x in nodos.Where(n => n.Id >= 0))
            {
                var (lo, hi) = DualBinaryPath.SubtreeBounds(x.Clave);
                EnRango(lo, hi).Should().BeEquivalentTo(Subarbol(x.Id), $"subarbol del nodo {x.Id} del arbol {arbol}");

                foreach (var lado in new[] { L, R })
                {
                    var hijo = hijos[x.Id].SingleOrDefault(h => h.Lado == lado);
                    var esperado = hijo.Clave is null ? new HashSet<int>() : Subarbol(hijo.Id);
                    var (llo, lhi) = DualBinaryPath.LegBounds(x.Clave, lado);
                    EnRango(llo, lhi).Should().BeEquivalentTo(esperado, $"pierna {lado} del nodo {x.Id}");
                }
            }

            var ordenadas = nodos.Select(n => n.Clave).ToList();
            ordenadas.Sort(DualBinaryPath.SqlCompare);
            for (var i = 1; i < ordenadas.Count; i++)
                DualBinaryPath.SqlCompare(ordenadas[i - 1], ordenadas[i]).Should().BeLessThan(0, "no hay dos claves iguales para SQL Server");
        }
    }

    [Fact]
    public void Rebase_lleva_un_descendiente_a_la_nueva_posicion_de_su_subarbol()
    {
        // B en /raiz1/R/L, con descendiente B-L-R-R. B pasa a /raiz9/L/L/L (otra raiz, otra profundidad).
        var bViejo = DualBinaryPath.Child(DualBinaryPath.Child(DualBinaryPath.Root(1), R), L);
        var d = bViejo;
        foreach (var l in new[] { L, R, R }) d = DualBinaryPath.Child(d, l);
        var bNuevo = DualBinaryPath.Root(9);
        foreach (var l in new[] { L, L, L }) bNuevo = DualBinaryPath.Child(bNuevo, l);
        var esperado = bNuevo;
        foreach (var l in new[] { L, R, R }) esperado = DualBinaryPath.Child(esperado, l);

        Hex(DualBinaryPath.Rebase(d, bViejo, bNuevo)).Should().Be(Hex(esperado));
        Hex(DualBinaryPath.Rebase(bViejo, bViejo, bNuevo)).Should().Be(Hex(bNuevo));
    }

    [Fact]
    public void Rebase_de_una_clave_fuera_del_subarbol_lanza()
    {
        var a = DualBinaryPath.Child(DualBinaryPath.Root(1), L);
        var b = DualBinaryPath.Child(DualBinaryPath.Root(1), R);
        var act = () => DualBinaryPath.Rebase(b, a, DualBinaryPath.Root(2));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rebase_profundo_conserva_la_forma()
    {
        var azar = new Random(7);
        var viejo = DualBinaryPath.Root(3);
        for (var i = 0; i < 500; i++) viejo = DualBinaryPath.Child(viejo, azar.Next(2) == 0 ? L : R);
        var lados = Enumerable.Range(0, 3000).Select(_ => azar.Next(2) == 0 ? L : R).ToList();
        var d = viejo;
        foreach (var l in lados) d = DualBinaryPath.Child(d, l);
        var nuevo = DualBinaryPath.Root(4);
        for (var i = 0; i < 4000; i++) nuevo = DualBinaryPath.Child(nuevo, R);
        var esperado = nuevo;
        foreach (var l in lados) esperado = DualBinaryPath.Child(esperado, l);

        Hex(DualBinaryPath.Rebase(d, viejo, nuevo)).Should().Be(Hex(esperado));
        DualBinaryPath.Depth(DualBinaryPath.Rebase(d, viejo, nuevo)).Should().Be(7000);
    }
}
