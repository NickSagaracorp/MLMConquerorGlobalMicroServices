namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Prefijo de subárbol para <c>HierarchyPath.StartsWith</c>, en el árbol de inscripción
/// (GenealogyTree) y en el binario (DualTeamTree). Las rutas tienen la forma "/A/B/C/"; el prefijo
/// de un nodo es su ruta y TIENE que acabar en "/": sin la barra, "/AMB-1" casaría también con
/// "/AMB-10/..." y metería en el equipo (o dejaría pasar una guarda de propiedad) a quien no lo es.
/// Hoy todas las rutas guardadas acaban en "/", pero el alcance no puede depender de eso: este es
/// el único sitio que lo garantiza (Plan J, revisión de los Bloques 3 y 4).
/// </summary>
public static class TreePrefix
{
    public static string Of(string hierarchyPath)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
            throw new ArgumentException("Una ruta vacía no es un prefijo de subárbol.", nameof(hierarchyPath));

        return hierarchyPath.EndsWith('/') ? hierarchyPath : hierarchyPath + "/";
    }
}
