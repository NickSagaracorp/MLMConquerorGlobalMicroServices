using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Security;

/// <summary>
/// QUIÉN ENTRA A LAS SUPERFICIES DE SOPORTE, en el servidor y no solo en el menú.
/// </summary>
/// <remarks>
/// EL AGUJERO. Los seis controladores de este servicio llevaban un <c>[Authorize]</c> pelado
/// —comprueba que HAY sesión y nunca de quién es— y toda la autorización vivía dentro de los
/// manejadores. Cuatro rutas de personal no tenían comprobación NINGUNA:
/// <c>GET /helpdesk/admin/teams</c>, <c>GET /helpdesk/sla/policies</c>,
/// <c>GET /helpdesk/canned-responses</c> y <c>POST /helpdesk/canned-responses/apply</c> —sus
/// manejadores ni siquiera inyectan <c>ICurrentUserService</c>—. Con el token de un miembro
/// cualquiera se leían los equipos de soporte con su supervisor, la matriz de SLA entera y el
/// texto de todas las plantillas internas de respuesta.
///
/// DE DÓNDE SALEN ESTAS DOS LISTAS, que es lo que hace que no sean inventadas: son EXACTAMENTE las
/// que ya declaran las páginas de AdminWeb que consumen estas rutas. La puerta ya estaba decidida;
/// lo único que pasaba es que solo la guardaba el navegador.
///
///   • <see cref="Soporte"/> = <c>AdminCannedResponses.razor</c>, <c>AdminKnowledgeBase.razor</c> y
///     <c>AdminHelpdeskDashboard.razor</c>.
///   • <see cref="Coordinacion"/> = <c>AdminSlaPolicies.razor</c> y <c>AdminSupportAgents.razor</c>.
///
/// Se componen de las constantes de <see cref="AppRoles"/> y no se escriben a mano: el atributo
/// exige una constante de compilación, y la concatenación de <c>const</c> lo es.
///
/// LO QUE NO SE TOCA Y POR QUÉ. <c>TicketsController</c> se queda con su <c>[Authorize]</c> pelado:
/// mezcla autoservicio del miembro dueño del ticket —crear, comentar, adjuntar, cerrar, puntuar—
/// con operaciones de personal —asignar, fusionar, escalar—, y sus manejadores ya distinguen las
/// dos con la propiedad del ticket. Cualquier lista de roles a nivel de clase dejaría fuera al
/// miembro que abre su propio ticket. No hay superficie equivalente de la que copiar una lista
/// para una clase mixta, así que no se inventa una: queda reportado.
///
/// Y LAS LECTURAS DE LA BASE DE CONOCIMIENTO tampoco: <c>search</c>, <c>suggestions</c> y
/// <c>articles/{slug}</c> filtran por visibilidad —un no-agente solo ve lo <c>Public</c>— y ese es
/// el nivel pensado para el centro de ayuda del miembro. Cerrarlas por rol convertiría el centro de
/// ayuda en una pantalla de personal.
/// </remarks>
public static class HelpdeskRoles
{
    /// <summary>
    /// Todo el personal de soporte. La misma lista de las tres páginas de plantillas, base de
    /// conocimiento y tablero.
    /// </summary>
    public const string Soporte =
        AppRoles.SuperAdmin + "," + AppRoles.Admin + "," +
        AppRoles.SupportManager + "," +
        AppRoles.SupportLevel1 + "," + AppRoles.SupportLevel2 + "," + AppRoles.SupportLevel3 + "," +
        AppRoles.IT;

    /// <summary>
    /// Quien configura el soporte, no quien lo ejerce: políticas de SLA, equipos y agentes. La
    /// misma lista de las dos páginas de configuración; los niveles 1 a 3 quedan fuera ahí y
    /// quedan fuera aquí.
    /// </summary>
    public const string Coordinacion =
        AppRoles.SuperAdmin + "," + AppRoles.Admin + "," +
        AppRoles.SupportManager + "," + AppRoles.IT;
}
