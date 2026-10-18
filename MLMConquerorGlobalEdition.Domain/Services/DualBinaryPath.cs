using System.Buffers.Binary;
using System.Numerics;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Services;

/// <summary>
/// Plan P: clave compacta del binario. R (ordinal de raiz, 4 bytes big-endian) + 1 bit por nivel
/// (izquierda 0, derecha 1) + bit terminador 1 + ceros hasta el byte. SQL Server ignora los ceros
/// finales al comparar varbinary, asi que el orden es el de las fracciones binarias: inorden.
/// Subarbol de X = (Lo, Hi) exclusivo; piernas = (Lo, X) y (X, Hi). Demostracion en el Plan P, Tarea 9.
/// </summary>
public static class DualBinaryPath
{
    public const int MaxBytes = 1700;
    public const int RootBytes = 4;
    public const int MaxDepth = (MaxBytes - RootBytes) * 8 - 1;   // 13.567
    public const int WarnDepth = 12_000;

    public static byte[] Root(int ordinal)
    {
        var k = new byte[RootBytes + 1];
        BinaryPrimitives.WriteInt32BigEndian(k, ordinal);
        k[RootBytes] = 0x80;
        return k;
    }

    public static int RootOrdinal(ReadOnlySpan<byte> key) => BinaryPrimitives.ReadInt32BigEndian(key);

    /// <summary>Numero de niveles bajo la raiz (0 = raiz): la posicion del terminador.</summary>
    public static int Depth(ReadOnlySpan<byte> key)
    {
        var last = key.Length - 1;
        if (last < RootBytes || key[last] == 0) throw new ArgumentException("Not a dual-tree key.", nameof(key));
        return (last - RootBytes) * 8 + 7 - BitOperations.TrailingZeroCount((uint)key[last]);
    }

    public static byte[] Child(ReadOnlySpan<byte> parent, TreeSide side)
    {
        var n = Depth(parent);
        if (n + 1 > MaxDepth) throw new DualTreeTooDeepException(n + 1);
        var k = new byte[RootBytes + (n + 1) / 8 + 1];
        parent.CopyTo(k);
        SetBit(k, n, side == TreeSide.Right);   // el terminador del padre pasa a ser el bit del lado
        SetBit(k, n + 1, true);                 // terminador del hijo
        return k;
    }

    /// <summary>Cotas exclusivas del subarbol de <paramref name="key"/> (el incluido).</summary>
    public static (byte[] Lo, byte[] Hi) SubtreeBounds(ReadOnlySpan<byte> key)
    {
        var n = Depth(key);
        var lo = key.ToArray();
        SetBit(lo, n, false);                   // R ‖ p ‖ 0…
        var hi = (byte[])lo.Clone();
        var bit = RootBytes * 8 + n - 1;        // ultimo bit de p; con n = 0, el ultimo bit de R
        var i = bit / 8;
        var add = 0x80 >> (bit % 8);
        while (i >= 0)
        {
            var v = hi[i] + add;
            hi[i] = (byte)v;
            if (v <= 0xFF) break;
            add = 1; i--;
        }
        return (lo, hi);
    }

    /// <summary>Cotas exclusivas de una pierna de <paramref name="key"/>.</summary>
    public static (byte[] Lo, byte[] Hi) LegBounds(ReadOnlySpan<byte> key, TreeSide side)
    {
        var (lo, hi) = SubtreeBounds(key);
        return side == TreeSide.Left ? (lo, key.ToArray()) : (key.ToArray(), hi);
    }

    /// <summary>
    /// La clave de <paramref name="key"/> cuando su subarbol, cuya raiz tenia <paramref name="oldRoot"/>, pasa a
    /// colgar con la clave <paramref name="newRoot"/>: los bits de camino por debajo de la raiz del subarbol se
    /// copian tal cual detras de la clave nueva. O(profundidad), sin claves intermedias.
    /// </summary>
    public static byte[] Rebase(ReadOnlySpan<byte> key, ReadOnlySpan<byte> oldRoot, ReadOnlySpan<byte> newRoot)
    {
        var dk = Depth(key);
        var d0 = Depth(oldRoot);
        var d1 = Depth(newRoot);
        if (dk < d0 || !Contains(oldRoot.ToArray(), key.ToArray()))
            throw new ArgumentException("The key is not inside the old subtree.", nameof(key));
        var nd = d1 + (dk - d0);
        if (nd > MaxDepth) throw new DualTreeTooDeepException(nd);

        var k = new byte[RootBytes + nd / 8 + 1];
        newRoot.CopyTo(k);
        SetBit(k, d1, false);                   // el terminador de la raiz nueva deja de serlo
        for (var i = 0; i < dk - d0; i++)
            if (GetBit(key, d0 + i)) SetBit(k, d1 + i, true);
        SetBit(k, nd, true);
        return k;
    }

    private static bool GetBit(ReadOnlySpan<byte> k, int pathBit) =>
        (k[RootBytes + pathBit / 8] & (0x80 >> (pathBit % 8))) != 0;

    /// <summary>¿Esta <paramref name="candidate"/> en el subarbol de <paramref name="root"/> (el incluido)?</summary>
    public static bool Contains(byte[] root, byte[] candidate)
    {
        var (lo, hi) = SubtreeBounds(root);
        return SqlCompare(candidate, lo) > 0 && SqlCompare(candidate, hi) < 0;
    }

    /// <summary>Comparacion de varbinary como SQL Server (los ceros finales son relleno).</summary>
    public static int SqlCompare(byte[]? a, byte[]? b)
    {
        a ??= Array.Empty<byte>();
        b ??= Array.Empty<byte>();
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }

    private static void SetBit(byte[] k, int pathBit, bool on)
    {
        var i = RootBytes + pathBit / 8;
        var mask = (byte)(0x80 >> (pathBit % 8));
        k[i] = on ? (byte)(k[i] | mask) : (byte)(k[i] & ~mask);
    }
}

public sealed class DualTreeTooDeepException(int depth)
    : Exception($"Dual-tree depth {depth} exceeds BinaryPath capacity ({DualBinaryPath.MaxDepth}).");
