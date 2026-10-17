namespace MLMConquerorGlobalEdition.Repository.Services.Events;

public static class TeamEventErrors
{
    public const string EventNotFound = "EVENT_NOT_FOUND";
}

public sealed class TeamEventView
{
    public string   Id        { get; set; } = string.Empty;
    public string   Title     { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string?  Location  { get; set; }
}

/// <summary>Un miembro del equipo y su inscripción (o no) a un evento.</summary>
public sealed class TeamEventRegistrationView
{
    public string    MemberId     { get; set; } = string.Empty;
    public string    FirstName    { get; set; } = string.Empty;
    public string    LastName     { get; set; } = string.Empty;
    public DateTime  EnrollDate   { get; set; }
    /// <summary>Calificado ahora (IQualificationService, corrección de Nick N3).</summary>
    public bool      IsQualified  { get; set; }
    public string?   RankName     { get; set; }
    public bool      Registered   { get; set; }
    /// <summary>La primera de sus compras para este evento.</summary>
    public DateTime? RegisteredAt { get; set; }
    /// <summary>Suma de las entradas de todas sus compras para este evento.</summary>
    public int       Tickets      { get; set; }
}

public sealed class TeamEventTotalsView
{
    /// <summary>Miembros del equipo con al menos una compra.</summary>
    public int Registered { get; set; }
    public int Tickets    { get; set; }
}
