namespace MLMConquerorGlobalEdition.Domain.Entities.Security;

/// <summary>
/// Política por operación crítica: si exige código, por qué canal, y cuántos minutos dura
/// la confirmación antes de volver a pedirla. Se siembra desde el catálogo en código al
/// arrancar; las claves que desaparecen del código se marcan obsoletas, nunca se borran,
/// porque los registros de auditoría las referencian.
/// </summary>
public class StepUpPolicy
{
    /// <summary>Clave estable, ej. "PAYOUT_BATCH_RELEASE". Es la PK.</summary>
    public string OperationKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public StepUpCategory Category { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Null = usar el canal preferido del usuario.</summary>
    public TwoFactorChannel? RequiredChannel { get; set; }

    /// <summary>0 = pedir código en cada operación, sin ventana.</summary>
    public int FreshnessWindowMinutes { get; set; } = 15;

    /// <summary>La clave ya no existe en el catálogo del código.</summary>
    public bool IsObsolete { get; set; }

    public DateTime? LastUpdateDate { get; set; }
    public string? LastUpdateBy { get; set; }
}
