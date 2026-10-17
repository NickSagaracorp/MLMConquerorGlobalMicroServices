namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Las tres vistas del reporte de cambios de estado (Plan J).</summary>
public enum StatusChangesScope
{
    /// <summary>Equipo de inscripción (prefijo de genealogía).</summary>
    Enrollment = 1,

    /// <summary>Árbol binario (se reduce por fecha y luego se comprueba la pertenencia).</summary>
    DualTeam = 2,

    /// <summary>Clientes (ExternalMember) del equipo de inscripción.</summary>
    Customers = 3
}

public static class StatusChangesScopes
{
    /// <summary>"enrollment", "dual" o "customers", sin distinguir mayúsculas.</summary>
    public static bool TryParse(string? value, out StatusChangesScope scope)
    {
        switch (value?.ToLowerInvariant())
        {
            case "enrollment": scope = StatusChangesScope.Enrollment; return true;
            case "dual":       scope = StatusChangesScope.DualTeam;   return true;
            case "customers":  scope = StatusChangesScope.Customers;  return true;
            default:           scope = default;                       return false;
        }
    }
}
