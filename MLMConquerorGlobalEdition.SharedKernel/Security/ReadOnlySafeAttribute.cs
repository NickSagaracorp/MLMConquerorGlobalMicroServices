namespace MLMConquerorGlobalEdition.SharedKernel.Security;

/// <summary>
/// Marca una ruta que USA un método de escritura pero solo LEE.
/// </summary>
/// <remarks>
/// POR QUÉ HACE FALTA. <see cref="ImpersonationScope"/> define "solo lectura" por el método HTTP,
/// que es lo único que no hay que mantener ruta a ruta. Pero hay lecturas que llegan por POST
/// porque el criterio de búsqueda no cabe en la barra de direcciones: las tres rejillas de
/// <c>TeamController</c> mandan filtro, orden y página en el cuerpo. Sin esta marca, una sesión de
/// suplantación de solo lectura no podría ver el equipo del miembro —justo lo que soporte necesita
/// mirar—, y "cerrar de más" también es romperle el trabajo a quien tenía derecho a hacerlo.
///
/// POR QUÉ ES UNA MARCA Y NO UNA LISTA EN OTRO ARCHIVO. La excepción se lee encima de la ruta que
/// la disfruta, se busca con un grep del nombre del atributo, y aparece en el diff de quien la
/// añade. Una lista de rutas en el middleware se desincroniza en cuanto alguien renombra una ruta,
/// y se desincroniza EN SILENCIO y ABRIENDO, que es la peor dirección.
///
/// CUÁNDO PONERLA: solo si la ruta no escribe NADA —ni una fila, ni un contador, ni una cola—.
/// Ante la duda, no se pone: el coste de no ponerla es que soporte no ve una pantalla; el de
/// ponerla mal es que una sesión declarada de solo lectura escribe.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class ReadOnlySafeAttribute : Attribute
{
}
