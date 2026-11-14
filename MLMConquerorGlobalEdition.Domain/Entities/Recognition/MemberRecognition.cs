using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>
/// Un Ambassador alcanzó un nivel de Club Recognition. Una fila por Ambassador y nivel; una vez creada no se borra aunque
/// después gane menos. El admin captura el reconocimiento: CEO Call = un mes (<see cref="CeoCallMonth"/>, día 1) o una
/// opción; evento = un <see cref="CorporateEvent"/> o una opción; talla del premio y notas. Cada cambio se audita.
/// </summary>
public class MemberRecognition : AuditChangesLongKey
{
    public string            MemberId           { get; set; } = string.Empty;
    public int               RecognitionLevelId { get; set; }
    public DateTime          ReachedDate        { get; set; }
    public decimal           QualifyingTotal    { get; set; }
    public RecognitionSource Source             { get; set; }

    public DateTime? CeoCallMonth    { get; set; }
    public int?      CeoCallOptionId { get; set; }
    public string?   CeoCallReason   { get; set; }

    public string? EventId       { get; set; }
    public int?    EventOptionId { get; set; }
    public string? EventReason   { get; set; }

    public string? RewardSize { get; set; }
    public string? Notes      { get; set; }

    public DateTime? LastUpdateDate { get; set; }
    public string?   LastUpdateBy   { get; set; }

    public RecognitionLevel?  Level         { get; set; }
    public RecognitionOption? CeoCallOption { get; set; }
    public RecognitionOption? EventOption   { get; set; }
    public CorporateEvent?    Event         { get; set; }
}
