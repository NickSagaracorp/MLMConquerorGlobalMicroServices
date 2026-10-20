namespace MLMConquerorGlobalEdition.Repository.Grid;

/// <summary>
/// Plan Q (DQ7, P7 del Plan P): por encima de <see cref="FreeMaxRows"/>, Mi equipo solo ordena por estructura (Node o
/// BinaryPath, un recorrido del indice) y solo busca por nombre o id por prefijo (MemberNameSearch). Ordenar o buscar por
/// subcadena un rango de un millon de filas tarda de 2 a 18 s (Plan P, Bloque D).
/// </summary>
public static class TeamGridLimits
{
    public const int FreeMaxRows = 20_000;

    /// <summary>True si el equipo supera el limite: quita los ordenes pedidos y quien llama ordena por estructura.</summary>
    public static bool Limit(GridDataRequest request, int teamSize)
    {
        if (teamSize <= FreeMaxRows) return false;
        request.Sorts = new List<GridSort>();
        return true;
    }
}
