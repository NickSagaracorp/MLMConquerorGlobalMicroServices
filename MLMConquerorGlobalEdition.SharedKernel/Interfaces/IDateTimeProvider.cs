namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Fuente única de la hora, inyectable para poder fijarla en pruebas.
///
/// La convención del sistema es <b>hora del servidor</b>: <see cref="Now"/> es lo que usa
/// toda la lógica de negocio —comisiones, rangos, facturación, auditoría— y es lo que se
/// persiste en la base de datos. Así las fechas se leen directamente en SQL y en los
/// informes sin conversiones mentales.
///
/// <see cref="UtcNow"/> existe para los pocos sitios donde el formato lo exige y no es
/// negociable: los tiempos de un JWT (<c>nbf</c>, <c>exp</c>, <c>iat</c>) son epoch UTC por
/// especificación, y cualquier comparación contra ellos tiene que hacerse en UTC. Meter
/// hora local en un JWT lo sella con el desfase del huso: en un servidor UTC−4, un token de
/// cinco minutos nace expirado hace casi cuatro horas.
///
/// Regla práctica: si el valor se guarda en nuestra base o se compara con otro valor
/// nuestro, usa <see cref="Now"/>. Si cruza un límite de protocolo, usa <see cref="UtcNow"/>.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Hora del servidor. El valor por defecto para lógica de negocio y persistencia.</summary>
    DateTime Now { get; }

    /// <summary>Hora UTC. Solo para formatos que la exigen, como los tiempos de un JWT.</summary>
    DateTime UtcNow { get; }
}
