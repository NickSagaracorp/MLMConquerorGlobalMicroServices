namespace MLMConquerorGlobalEdition.Domain.Services;

/// <summary>
/// Plan Q (DQ5): la linea de inscripcion que recibe avisos y evaluaciones de rango inmediatas, acotada. La misma cota que I3
/// del Plan P (avisos de rango). Mas alla, la fase 2 de RankEvaluationSweepJob evalua a todos cada noche.
/// </summary>
public static class GenealogyUpline
{
    public const int EnrollmentLevels = 10;

    /// <summary>
    /// Los ascendientes que nombra una ruta de inscripcion "/A/B/C/", del mas cercano al mas lejano, sin
    /// <paramref name="selfMemberId"/> y como mucho <paramref name="levels"/>.
    /// </summary>
    public static IReadOnlyList<string> Closest(string? hierarchyPath, string? selfMemberId, int levels = EnrollmentLevels)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath) || levels <= 0) return Array.Empty<string>();
        return hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.Equals(s, selfMemberId, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Take(levels)
            .ToList();
    }
}
