namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Lo que inscribió un miembro del equipo en el rango. Los niveles son los ids reales de
/// MembershipLevels (1 Lifestyle Ambassador, 2 VIP, 3 Elite, 4 Turbo); "Plus" no existe aquí y
/// cualquier otro nivel, o ninguno, va a <see cref="OtherLevel"/> (Plan J, J10). El nivel de cada
/// inscrito es el más alto de los productos de su orden de alta (corrección de Nick N2).
/// </summary>
public sealed class EnrollmentActivityRowView
{
    public string MemberId             { get; set; } = string.Empty;
    public string FullName             { get; set; } = string.Empty;
    /// <summary>Nivel relativo al que mira (0 = él mismo).</summary>
    public int    Level                { get; set; }
    public int    Ambassadors          { get; set; }
    public int    Customers            { get; set; }
    public int    Level1               { get; set; }
    public int    Level2               { get; set; }
    public int    Level3               { get; set; }
    public int    Level4               { get; set; }
    public int    OtherLevel           { get; set; }
    /// <summary>MemberStatistics.EnrollmentPoints actuales, como el legado.</summary>
    public int    EnrollmentTeamPoints { get; set; }
}

/// <summary>Cabecera de una columna de nivel.</summary>
public sealed class EnrollmentActivityColumnView
{
    public int    LevelId { get; set; }
    public string Name    { get; set; } = string.Empty;
}
