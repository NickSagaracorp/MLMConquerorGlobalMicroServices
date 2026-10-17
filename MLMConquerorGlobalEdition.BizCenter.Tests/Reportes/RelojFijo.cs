using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Reloj de servidor fijo para las pruebas de reportes. Es el <c>IDateTimeProvider</c> de SharedKernel
/// (el que reciben los servicios de Repository), no el de <c>BizCenter.Services</c>.
/// </summary>
public sealed class RelojFijo : IDateTimeProvider
{
    public RelojFijo(DateTime now) => Now = now;

    public DateTime Now { get; set; }

    /// <summary>
    /// La hora fija tomada como UTC, sin convertir: ToUniversalTime() dependía de la zona horaria
    /// de la máquina que corre la prueba.
    /// </summary>
    public DateTime UtcNow => DateTime.SpecifyKind(Now, DateTimeKind.Utc);
}
