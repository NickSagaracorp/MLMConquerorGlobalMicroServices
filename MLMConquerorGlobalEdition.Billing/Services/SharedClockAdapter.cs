namespace MLMConquerorGlobalEdition.Billing.Services;

/// <summary>
/// El reloj de Billing visto como el de SharedKernel, para las piezas compartidas que lo piden
/// (StepUpGrantValidator). Billing tiene su propia interfaz con los mismos dos miembros; así hay un
/// solo reloj y no dos que puedan discrepar.
/// </summary>
public sealed class SharedClockAdapter : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
{
    private readonly IDateTimeProvider _inner;

    public SharedClockAdapter(IDateTimeProvider inner) => _inner = inner;

    public DateTime Now    => _inner.Now;
    public DateTime UtcNow => _inner.UtcNow;
}
