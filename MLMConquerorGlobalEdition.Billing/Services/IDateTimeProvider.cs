namespace MLMConquerorGlobalEdition.Billing.Services;

public interface IDateTimeProvider
{
    /// <summary>Hora del servidor. El valor por defecto para logica de negocio y persistencia.</summary>
    DateTime Now { get; }

    /// <summary>Hora UTC. Solo para formatos que la exigen, como los tiempos de un JWT.</summary>
    DateTime UtcNow { get; }
}
