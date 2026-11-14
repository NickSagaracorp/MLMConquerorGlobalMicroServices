using System.Globalization;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Las cinco vistas del menú Club Recognition. «Online» = CEO Call.</summary>
public enum ClubRecognitionView
{
    All,
    NotRecognizedOnline,
    NotRecognizedEvents,
    RecognizedOnline,
    RecognizedEvents
}

/// <summary>Vista, nivel (null = todos) y búsqueda por nombre, email o país.</summary>
public sealed record ClubRecognitionFilter(
    ClubRecognitionView View = ClubRecognitionView.All,
    int? LevelId = null,
    string? Search = null);

/// <summary>Nombre de cada vista en las rutas de AdminWeb y en el archivo CSV.</summary>
public static class ClubRecognitionViewNames
{
    public static string Slug(ClubRecognitionView view) => view switch
    {
        ClubRecognitionView.NotRecognizedOnline => "not-recognized-online",
        ClubRecognitionView.NotRecognizedEvents => "not-recognized-events",
        ClubRecognitionView.RecognizedOnline    => "recognized-online",
        ClubRecognitionView.RecognizedEvents    => "recognized-events",
        _                                       => "all"
    };
}

/// <summary>Textos de las columnas «CEO Call Recognized» y «Event Recognized». Null = no reconocido.</summary>
public static class RecognitionText
{
    /// <summary>Separador entre una opción y su motivo («Do not recognize – Sponsor request»).</summary>
    public const string ReasonSeparator = " – ";

    /// <summary>El mes como «10 2026» (mes y año, como en la herramienta del cliente) o la opción.</summary>
    public static string? CeoCall(DateTime? month, string? optionName, string? reason) =>
        month is not null ? month.Value.ToString("M yyyy", CultureInfo.InvariantCulture) : Option(optionName, reason);

    /// <summary>El título del evento o la opción.</summary>
    public static string? Event(string? eventTitle, string? optionName, string? reason) =>
        eventTitle ?? Option(optionName, reason);

    private static string? Option(string? name, string? reason) =>
        name is null ? null : string.IsNullOrWhiteSpace(reason) ? name : name + ReasonSeparator + reason;
}

/// <summary>Una fila de Club Recognition: vistas, exportación y pestaña del perfil.</summary>
public sealed class ClubRecognitionRowView
{
    public long              Id                 { get; init; }
    public string            MemberId           { get; init; } = string.Empty;
    public string            MemberName         { get; init; } = string.Empty;
    public string            Email              { get; init; } = string.Empty;
    public string?           Username           { get; init; }
    public string            Country            { get; init; } = string.Empty;
    public int               RecognitionLevelId { get; init; }
    public string            LevelName          { get; init; } = string.Empty;
    public int               LevelSortOrder     { get; init; }
    public string?           Reward             { get; init; }
    public bool              RewardNeedsSize    { get; init; }
    public DateTime          ReachedDate        { get; init; }
    public decimal           QualifyingTotal    { get; init; }
    public RecognitionSource Source             { get; init; }
    public DateTime?         CeoCallMonth       { get; init; }
    public int?              CeoCallOptionId    { get; init; }
    public string?           CeoCallReason      { get; init; }
    public string?           CeoCallRecognized  { get; init; }
    public string?           EventId            { get; init; }
    public int?              EventOptionId      { get; init; }
    public string?           EventReason        { get; init; }
    public string?           EventRecognized    { get; init; }
    public string?           RewardSize         { get; init; }
    public string?           Notes              { get; init; }
}
