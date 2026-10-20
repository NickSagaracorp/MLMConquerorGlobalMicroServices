using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>Adonde va un subarbol que se mueve.</summary>
public abstract record DualTeamDestination
{
    private DualTeamDestination() { }

    /// <summary>Bajo un nodo real, en un lado libre.</summary>
    public sealed record UnderParent(DualTeamEntity Parent, TreeSide Side) : DualTeamDestination;

    /// <summary>
    /// Bajo un padre que ya no tiene nodo (retirada del admin): el hijo conserva ParentMemberId y cuelga de
    /// una raiz virtual con su propio ordinal, hasta que el padre vuelve a colocarse.
    /// </summary>
    public sealed record Floating(string ParentMemberId, byte[] VirtualParentKey, TreeSide Side) : DualTeamDestination;

    /// <summary>Raiz nueva (provisional) con ordinal propio.</summary>
    public sealed record NewRoot : DualTeamDestination;
}

/// <summary>Que hacer con los hijos de un nodo que se retira.</summary>
public enum DualTeamChildrenOnRemoval
{
    /// <summary>No se retira si tiene hijos (SignupAPI, revision I7).</summary>
    Reject,
    /// <summary>Los hijos esperan bajo una raiz virtual del nodo retirado y vuelven con el (admin).</summary>
    Floating,
    /// <summary>Cada hijo pasa a ser raiz provisional con su subarbol (BizCenter).</summary>
    Roots,
}

/// <summary>
/// Plan P: el unico sitio que construye nodos del binario y reescribe su posicion (ParentMemberId, Side) y sus
/// claves (BinaryPath, Depth). Lo vigila EscritoresDelBinarioTests. Plan Q (Bloque 3): ya no escribe la ruta de texto.
/// <list type="bullet">
/// <item>Altas: <see cref="ChildAsync"/> y <see cref="RootAsync"/>; quien llama tiene el candado del hueco.</item>
/// <item>Movimientos y retiradas: <see cref="MoveSubtreeAsync"/> y <see cref="RemoveAsync"/>; exigen
/// <c>dual-tree</c> en exclusivo (DualTeamGate.EnterTreeExclusiveAsync) y lo comprueban. Las claves del
/// subarbol se calculan en C# (DualBinaryPath.Rebase) y se escriben de una vez; las piernas, encolando el subarbol con signo (LegPointMaintenance.EnqueueSubtreeAsync, Plan Q); y se vacian las
/// colas de derrame que apuntaban al punto donde se engancha o se suelta el subarbol. Las demas no hace falta
/// tocarlas: el motor valida la cola al leerla por la clave (sigue en la pierna, existe y tiene hueco) y la rehace
/// si no (recorrer los 8.000 ascendientes para vaciar las suyas costaba casi un segundo por movimiento).</item>
/// </list>
/// Fuera de SQL Server (InMemory, pruebas de logica) hace lo mismo con entidades; si un nodo sembrado sin clave
/// la necesita, se la calcula por adyacencia. En SQL Server, un nodo sin clave es un error (relleno pendiente).
/// </summary>
public static class DualTeamNodes
{
    private static int _ordinalEnMemoria;

    public static async Task<int> NextRootOrdinalAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlServer()) return Interlocked.Increment(ref _ordinalEnMemoria);
        var r = await db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.DualTeamRootOrdinal AS [Value]").ToListAsync(ct);
        return r[0];
    }

    /// <summary>Una raiz (raiz del sistema o raiz provisional de un patrocinador sin colocar).</summary>
    public static async Task<DualTeamEntity> RootAsync(AppDbContext db, string memberId, DateTime now, string createdBy,
        CancellationToken ct = default) => new()
    {
        MemberId = memberId, ParentMemberId = null, Side = TreeSide.Left,
        BinaryPath = DualBinaryPath.Root(await NextRootOrdinalAsync(db, ct)), Depth = 0,
        CreationDate = now, CreatedBy = createdBy, LastUpdateDate = now, LastUpdateBy = createdBy
    };

    /// <summary>Una hoja nueva bajo <paramref name="parent"/>.</summary>
    public static async Task<DualTeamEntity> ChildAsync(AppDbContext db, DualTeamEntity parent, string memberId, TreeSide side,
        DateTime now, string createdBy, CancellationToken ct = default)
    {
        var (key, depth) = await KeyOfAsync(db, parent, ct);
        AvisarSiHondo(db, memberId, depth + 1);
        return new DualTeamEntity
        {
            MemberId = memberId, ParentMemberId = parent.MemberId, Side = side,
            BinaryPath = DualBinaryPath.Child(key, side), Depth = depth + 1,
            CreationDate = now, CreatedBy = createdBy, LastUpdateDate = now, LastUpdateBy = createdBy
        };
    }

    /// <summary>
    /// Mueve el subarbol de <paramref name="rootId"/>, sin cambiar su forma, a <paramref name="to"/>. Con
    /// <paramref name="adjustLegs"/>, resta sus totales de la linea ascendente vieja (si la tenia) y los suma a
    /// la nueva. Lanza <see cref="InvalidOperationException"/> si el destino esta dentro del propio subarbol.
    /// </summary>
    public static async Task MoveSubtreeAsync(AppDbContext db, string rootId, DualTeamDestination to, bool adjustLegs,
        DateTime now, string by, CancellationToken ct = default)
    {
        await RequireTreeExclusiveAsync(db, ct);
        var root = await db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == rootId, ct);
        var (oldKey, _) = await KeyOfAsync(db, root, ct);
        var oldParent = root.ParentMemberId;

        string? newParentId;
        TreeSide newSide;
        byte[] newKey;
        switch (to)
        {
            case DualTeamDestination.UnderParent u:
                var parent = await db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == u.Parent.MemberId, ct);
                var (pKey, _) = await KeyOfAsync(db, parent, ct);
                if (parent.MemberId == rootId || DualBinaryPath.Contains(oldKey, pKey))
                    throw new InvalidOperationException($"{parent.MemberId} is inside the subtree of {rootId}: circular move.");
                (newParentId, newSide, newKey) = (parent.MemberId, u.Side, DualBinaryPath.Child(pKey, u.Side));
                break;
            case DualTeamDestination.Floating f:
                (newParentId, newSide, newKey) = (f.ParentMemberId, f.Side, DualBinaryPath.Child(f.VirtualParentKey, f.Side));
                break;
            default:
                (newParentId, newSide, newKey) = (null, TreeSide.Left, DualBinaryPath.Root(await NextRootOrdinalAsync(db, ct)));
                break;
        }

        // Revision C1: solo se resta de la linea vieja si el subarbol cuelga DE VERDAD de su padre, por la clave. Un
        // hijo flotante de una retirada del admin apunta a su padre por ParentMemberId, pero por la clave sigue bajo
        // la raiz virtual: si el padre acaba de recibir nodo, restar por adyacencia quitaba de la linea nueva lo que
        // despues se sumaba (neto 0, con piernas negativas por el camino).
        if (adjustLegs && await HangsFromParentByKeyAsync(db, oldParent, oldKey, ct))
            await LegPointMaintenance.EnqueueSubtreeAsync(db, rootId, -1, now, by, ct);

        if (db.Database.IsSqlServer())
            await RewriteSubtreeSqlAsync(db, root, oldKey, newKey, newParentId, newSide, now, by, ct);
        else
            await RewriteSubtreeInMemoryAsync(db, root, oldKey, newKey, newParentId, newSide, now, by, ct);

        if (adjustLegs && newParentId is not null && to is DualTeamDestination.UnderParent)
            await LegPointMaintenance.EnqueueSubtreeAsync(db, rootId, +1, now, by, ct);

        var maxima = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId == rootId).Select(d => d.Depth).FirstOrDefaultAsync(ct) ?? 0;
        if (db.Database.IsSqlServer())
        {
            // La hoja mas honda del subarbol movido (un Index Seek sobre el rango de su clave nueva).
            maxima = await DualSubtree.Under(db, new DualTeamEntity { MemberId = rootId, BinaryPath = newKey })
                .Select(d => d.Depth).MaxAsync(ct) ?? maxima;
        }
        AvisarSiHondo(db, rootId, maxima);

        await InvalidateFrontiersAsync(db, new[] { oldParent, newParentId }, ct);
        await ReloadTrackedAsync(db, ct);
    }

    /// <summary>
    /// Retira el nodo de <paramref name="memberId"/>: resta su subarbol de la linea ascendente, coloca a sus hijos
    /// segun <paramref name="children"/> y borra el nodo. Devuelve false (sin tocar nada) si
    /// <paramref name="children"/> es Reject y el nodo tiene hijos.
    /// </summary>
    public static async Task<bool> RemoveAsync(AppDbContext db, string memberId, DualTeamChildrenOnRemoval children,
        DateTime now, string by, CancellationToken ct = default)
    {
        await RequireTreeExclusiveAsync(db, ct);
        var node = await db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == memberId, ct);
        var hijos = await db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == memberId).OrderBy(d => d.Side).ToListAsync(ct);
        if (children == DualTeamChildrenOnRemoval.Reject && hijos.Count > 0) return false;

        // Plan Q: como en MoveSubtreeAsync, solo se resta si el nodo cuelga DE VERDAD de su padre, por la clave. Un hijo
        // flotante (su padre retirado no tiene nodo) no esta en ninguna linea: antes, la subida no encontraba al padre y
        // no hacia nada; un -T encolado con arranque en el padre esperaria en la cola y, si el padre se volviera a
        // colocar antes del vaciado, restaria de su nodo nuevo lo que ya no cuelga de el.
        var (clave, _) = await KeyOfAsync(db, node, ct);
        if (await HangsFromParentByKeyAsync(db, node.ParentMemberId, clave, ct))
            await LegPointMaintenance.EnqueueSubtreeAsync(db, memberId, -1, now, by, ct);
        // Plan Q (revision C1): los pendientes DE PIERNA con arranque en este nodo no estan en el -T que se acaba de
        // encolar (sale de las piernas ya aplicadas). En el valor verdadero se habrian sumado y restado; aqui se
        // descartan. Si se dejaran, al volver a colocar al mismo miembro antes del vaciado subirian por su nodo nuevo, y su
        // equipo ya vuelve con su propio +T: contaria dos veces.
        // Plan Q (DQ13): los PROPIOS son del miembro, no del nodo: se quedan, y lo ya aplicado de el (SelfQualified)
        // vuelve a la cola como propio. Si se le vuelve a colocar, su nodo nuevo (SelfQualified = 0) lo recibe al vaciar;
        // si no, espera.
        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM DualLegDeltas WHERE StartMemberId = {memberId} AND OwnChange = 0", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO DualLegDeltas (StartMemberId, Side, Points, NodeCount, QualifiedCount, OwnChange, CreationDate, CreatedBy)
SELECT d.MemberId, 0, 0, 0, d.SelfQualified, 1, {now}, {by}
FROM DualTeamTree d
WHERE d.MemberId = {memberId} AND d.IsDeleted = 0 AND d.SelfQualified <> 0;", ct);
        }
        await InvalidateFrontiersAsync(db, new[] { node.ParentMemberId, memberId }, ct);

        if (children == DualTeamChildrenOnRemoval.Floating && hijos.Count > 0)
        {
            var virtualKey = DualBinaryPath.Root(await NextRootOrdinalAsync(db, ct));
            foreach (var h in hijos)
                await MoveSubtreeAsync(db, h.MemberId, new DualTeamDestination.Floating(memberId, virtualKey, h.Side), adjustLegs: false, now, by, ct);
        }
        else if (children == DualTeamChildrenOnRemoval.Roots)
        {
            foreach (var h in hijos)
                await MoveSubtreeAsync(db, h.MemberId, new DualTeamDestination.NewRoot(), adjustLegs: false, now, by, ct);
        }

        var fila = await db.DualTeamTree.FirstAsync(d => d.MemberId == memberId, ct);
        db.DualTeamTree.Remove(fila);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Revision I1: tras dar nodo a <paramref name="memberId"/>, recuelga bajo el a los hijos que lo esperaban de una
    /// retirada del admin (<paramref name="floating"/>, leidos con <see cref="FloatingChildrenAsync"/> ANTES de crear su
    /// nodo), cada uno por su lado y con su subarbol: claves y piernas (su volumen sube a la linea nueva).
    /// Exige dual-tree en exclusivo, asi que quien coloca a un miembro con flotantes toma el exclusivo desde el
    /// principio. La llaman todos los caminos que crean la hoja: motor, BizCenter, admin y SignupAPI.
    /// </summary>
    public static async Task ReattachFloatingChildrenAsync(AppDbContext db, string memberId, IReadOnlyList<DualTeamEntity> floating,
        DateTime now, string by, CancellationToken ct = default)
    {
        if (floating.Count == 0) return;
        await RequireTreeExclusiveAsync(db, ct);
        var nodo = await db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == memberId, ct);
        foreach (var hijo in floating)
            await MoveSubtreeAsync(db, hijo.MemberId, new DualTeamDestination.UnderParent(nodo, hijo.Side), adjustLegs: true, now, by, ct);
    }

    /// <summary>¿Cuelga <paramref name="key"/> de verdad de <paramref name="parentId"/>, por la clave (no solo por ParentMemberId)?</summary>
    private static async Task<bool> HangsFromParentByKeyAsync(AppDbContext db, string? parentId, byte[] key, CancellationToken ct)
    {
        if (parentId is null) return false;
        var padre = await db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == parentId, ct);
        if (padre is null) return false;
        var (pk, _) = await KeyOfAsync(db, padre, ct);
        return DualBinaryPath.SqlCompare(pk, key) != 0 && DualBinaryPath.Contains(pk, key);
    }

    /// <summary>
    /// Los hijos que esperan a <paramref name="memberId"/> tras una retirada del admin (ParentMemberId apunta a el
    /// pero no tiene nodo). Se recolocan bajo su nodo nuevo con <see cref="MoveSubtreeAsync"/>.
    /// </summary>
    public static Task<List<DualTeamEntity>> FloatingChildrenAsync(AppDbContext db, string memberId, CancellationToken ct = default) =>
        db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == memberId && !db.DualTeamTree.Any(p => p.MemberId == memberId))
            .OrderBy(d => d.Side).ToListAsync(ct);

    /// <summary>
    /// Vacia las colas de derrame que dependian de los nodos <paramref name="ids"/> (un punto de enganche o de suelta):
    /// la cola del propio nodo y las que acababan en el. El resto se valida al leerla (PlacementService).
    /// </summary>
    public static async Task InvalidateFrontiersAsync(AppDbContext db, IEnumerable<string?> ids, CancellationToken ct = default)
    {
        var lista = ids.Where(i => i is not null).Select(i => i!).Distinct().ToList();
        if (lista.Count == 0) return;
        var colas = await db.DualTeamLegFrontiers
            .Where(f => lista.Contains(f.SponsorMemberId) || (f.DeepestMemberId != null && lista.Contains(f.DeepestMemberId)))
            .ToListAsync(ct);
        if (colas.Count == 0) return;
        db.DualTeamLegFrontiers.RemoveRange(colas);
        await db.SaveChangesAsync(ct);
    }

    private static async Task RewriteSubtreeSqlAsync(AppDbContext db, DualTeamEntity root, byte[] oldKey, byte[] newKey,
        string? newParentId, TreeSide newSide, DateTime now, string by, CancellationToken ct)
    {
        var (lo, hi) = DualBinaryPath.SubtreeBounds(oldKey);
        var conn = (SqlConnection)db.Database.GetDbConnection();
        var tx = (SqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();

        var nuevas = new DataTable();
        nuevas.Columns.Add("MemberId", typeof(string));
        nuevas.Columns.Add("Clave", typeof(byte[]));
        nuevas.Columns.Add("Prof", typeof(int));
        await using (var leer = new SqlCommand(
            "SELECT MemberId, BinaryPath FROM DualTeamTree WHERE BinaryPath > @lo AND BinaryPath < @hi AND IsDeleted = 0", conn, tx))
        {
            leer.Parameters.Add("@lo", SqlDbType.VarBinary, DualBinaryPath.MaxBytes).Value = lo;
            leer.Parameters.Add("@hi", SqlDbType.VarBinary, DualBinaryPath.MaxBytes).Value = hi;
            await using var r = await leer.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var k = DualBinaryPath.Rebase((byte[])r[1], oldKey, newKey);
                nuevas.Rows.Add(r.GetString(0), k, DualBinaryPath.Depth(k));
            }
        }

        await using (var crear = new SqlCommand(
            "CREATE TABLE #mv (MemberId nvarchar(450) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY, Clave varbinary(1700) NOT NULL, Prof int NOT NULL)", conn, tx))
            await crear.ExecuteNonQueryAsync(ct);
        try
        {
            using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tx) { DestinationTableName = "#mv", BulkCopyTimeout = 0 })
                await bulk.WriteToServerAsync(nuevas, ct);

            await using var escribir = new SqlCommand("""
                UPDATE d SET BinaryPath = m.Clave, Depth = m.Prof,
                             LastUpdateDate = @now, LastUpdateBy = @by
                FROM DualTeamTree d JOIN #mv m ON m.MemberId = d.MemberId
                WHERE d.IsDeleted = 0;
                UPDATE DualTeamTree SET ParentMemberId = @padre, Side = @lado
                WHERE MemberId = @raiz AND IsDeleted = 0;
                """, conn, tx) { CommandTimeout = 0 };
            escribir.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;
            escribir.Parameters.Add("@by", SqlDbType.NVarChar, -1).Value = by;
            escribir.Parameters.Add("@padre", SqlDbType.NVarChar, 900).Value = (object?)newParentId ?? DBNull.Value;
            escribir.Parameters.Add("@lado", SqlDbType.Int).Value = (int)newSide;
            escribir.Parameters.Add("@raiz", SqlDbType.NVarChar, 900).Value = root.MemberId;
            await escribir.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            // Revision (menor 2): si la transaccion murio (un 1205), este DROP fallaria y taparia la excepcion original,
            // que es la que PlacementService reconoce para reintentar. Se borra solo si existe y su error se traga:
            // la tabla temporal muere con la sesion de todos modos.
            try
            {
                await using var borrar = new SqlCommand("IF OBJECT_ID('tempdb..#mv') IS NOT NULL DROP TABLE #mv", conn, tx);
                await borrar.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (Exception)
            {
            }
        }
    }

    private static async Task RewriteSubtreeInMemoryAsync(AppDbContext db, DualTeamEntity root, byte[] oldKey, byte[] newKey,
        string? newParentId, TreeSide newSide, DateTime now, string by, CancellationToken ct)
    {
        var raiz = await db.DualTeamTree.FirstAsync(d => d.MemberId == root.MemberId, ct);
        var subarbol = new List<DualTeamEntity> { raiz };
        for (var i = 0; i < subarbol.Count; i++)
        {
            var id = subarbol[i].MemberId;
            subarbol.AddRange(await db.DualTeamTree.Where(d => d.ParentMemberId == id).ToListAsync(ct));
        }
        // Primero las claves que falten (calculadas desde la posicion vieja) y despues, todas, a la nueva.
        foreach (var n in subarbol)
            if (n.BinaryPath is null) (n.BinaryPath, n.Depth) = await KeyOfAsync(db, n, ct);
        foreach (var n in subarbol)
        {
            n.BinaryPath = DualBinaryPath.Rebase(n.BinaryPath!, oldKey, newKey);
            n.Depth = DualBinaryPath.Depth(n.BinaryPath);
            n.LastUpdateDate = now;
            n.LastUpdateBy = by;
        }
        raiz.ParentMemberId = newParentId;
        raiz.Side = newSide;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// La clave de un nodo. En SQL Server la tiene que tener (el relleno es previo al despliegue). En InMemory, un
    /// nodo sembrado sin clave la recibe aqui, calculada por adyacencia hasta su raiz.
    /// </summary>
    private static async Task<(byte[] Key, int Depth)> KeyOfAsync(AppDbContext db, DualTeamEntity node, CancellationToken ct)
    {
        if (node.BinaryPath is not null) return (node.BinaryPath, node.Depth ?? DualBinaryPath.Depth(node.BinaryPath));
        if (db.Database.IsRelational())
            throw new InvalidOperationException($"{node.MemberId} has no BinaryPath (backfill pending: Scripts/sql/plan-p/03-relleno-clave-binario.sql).");

        var cadena = new List<DualTeamEntity>();
        var actual = await db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == node.MemberId, ct) ?? node;
        while (true)
        {
            cadena.Add(actual);
            if (actual.BinaryPath is not null || actual.ParentMemberId is null) break;
            var padre = await db.DualTeamTree.FirstOrDefaultAsync(d => d.MemberId == actual.ParentMemberId, ct);
            if (padre is null || cadena.Contains(padre)) break;
            actual = padre;
        }
        cadena.Reverse();
        var clave = cadena[0].BinaryPath ?? DualBinaryPath.Root(Interlocked.Increment(ref _ordinalEnMemoria));
        cadena[0].BinaryPath = clave;
        cadena[0].Depth = DualBinaryPath.Depth(clave);
        for (var i = 1; i < cadena.Count; i++)
        {
            clave = DualBinaryPath.Child(clave, cadena[i].Side);
            cadena[i].BinaryPath = clave;
            cadena[i].Depth = DualBinaryPath.Depth(clave);
        }
        node.BinaryPath = cadena[^1].BinaryPath;
        node.Depth = cadena[^1].Depth;
        return (node.BinaryPath!, node.Depth!.Value);
    }

    /// <summary>
    /// Riesgo P-R1: por encima de DualBinaryPath.WarnDepth (12.000) niveles queda poco margen hasta el limite de la
    /// clave (13.567, donde la colocacion falla cerrada). Se avisa en el log de la aplicacion (el del contexto).
    /// </summary>
    private static void AvisarSiHondo(AppDbContext db, string memberId, int profundidad)
    {
        if (profundidad <= DualBinaryPath.WarnDepth) return;
        db.GetService<ILoggerFactory>().CreateLogger("MLMConquerorGlobalEdition.DualTeamNodes").LogWarning(
            "Dual tree: {MemberId} quedo a {Depth} niveles (aviso a partir de {WarnDepth}; limite de la clave {MaxDepth}).",
            memberId, profundidad, DualBinaryPath.WarnDepth, DualBinaryPath.MaxDepth);
    }

    private static async Task RequireTreeExclusiveAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;
        var modo = await db.Database.SqlQuery<string>(
            $"SELECT APPLOCK_MODE(N'public', {DualTeamGate.TreeResource}, N'Transaction') AS [Value]").ToListAsync(ct);
        if (modo.Count != 1 || modo[0] != "Exclusive")
            throw new InvalidOperationException("Moving or removing a dual-tree subtree needs DualTeamGate.EnterTreeExclusiveAsync.");
    }

    private static async Task ReloadTrackedAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var e in db.ChangeTracker.Entries<DualTeamEntity>().ToList())
            if (e.State is EntityState.Unchanged or EntityState.Modified) await e.ReloadAsync(ct);
    }
}
