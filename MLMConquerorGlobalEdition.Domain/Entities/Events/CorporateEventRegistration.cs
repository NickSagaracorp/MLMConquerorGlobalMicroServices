using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

/// <summary>
/// Una compra de entradas de un miembro para un evento corporativo (Plan J, J18). Es la fuente del
/// reporte de eventos del equipo. Todavía no hay importación (corrección de Nick N5): llegarán por
/// importación CSV desde el Admin en un plan aparte. La referencia externa es la de la compra en la
/// plataforma de entradas y es única por evento, para que esa importación sea idempotente.
/// </summary>
public class CorporateEventRegistration : AuditChangesLongKey
{
    public const string CsvImportSource = "CsvImport";

    public string    EventId      { get; set; } = string.Empty;
    public string    MemberId     { get; set; } = string.Empty;
    public DateTime  RegisteredAt { get; set; }
    public int       Tickets      { get; set; }
    public decimal?  Price        { get; set; }
    public string    Source       { get; set; } = CsvImportSource;
    public string    ExternalRef  { get; set; } = string.Empty;
}
