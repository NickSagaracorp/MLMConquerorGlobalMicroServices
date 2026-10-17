namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Qué clientes del equipo de inscripción se listan (Plan J).</summary>
public enum CustomerScope
{
    /// <summary>Solo los que inscribió directamente el que mira (SponsorMemberId == él).</summary>
    Personal = 1,

    /// <summary>Todo el subárbol de inscripción. El contacto solo se ve de los personales.</summary>
    Team = 2
}

public static class CustomerScopes
{
    /// <summary>"personal" (sin distinguir mayúsculas) es Personal; cualquier otra cosa, Team.</summary>
    public static CustomerScope Parse(string? value) =>
        string.Equals(value, "personal", StringComparison.OrdinalIgnoreCase)
            ? CustomerScope.Personal
            : CustomerScope.Team;
}
