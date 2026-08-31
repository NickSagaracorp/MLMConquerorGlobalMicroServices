using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Pone el TEXTO de los correos de enlace de acuerdo con su caducidad real: los tokens de
    /// confirmación de dirección y de recuperación de contraseña pasan de un día a 15 minutos, y las
    /// plantillas seguían anunciando 24 horas.
    /// </summary>
    /// <remarks>
    /// POR QUÉ HACE FALTA UNA MIGRACIÓN. Los cuerpos de estos correos no están en el código: se
    /// sembraron como filas de <c>EmailTemplateLocalizations</c> en
    /// <c>SeedEmailConfirmationTemplate</c> y <c>SeedPasswordResetTemplate</c>, y desde entonces son
    /// datos de la base. Cambiar la vigencia sin tocarlos habría dejado el peor resultado posible:
    /// un enlace que muere a los 15 minutos con un correo que promete 24 horas, leído por alguien
    /// que está bloqueado fuera de su cuenta y ya no sabe si el problema es el enlace o él.
    ///
    /// POR QUÉ CAMBIA TAMBIÉN EL NOMBRE DE LA VARIABLE, y no solo el número. La variable se llamaba
    /// <c>{{ExpiresInHours}}</c>. Dejarla con un 15 dentro habría hecho que la plantilla dijera "15"
    /// en una casilla que se llama "horas": el mismo error que se está corrigiendo, ahora escondido
    /// en el nombre en vez de en el texto. Pasa a <c>{{ExpiresInMinutes}}</c>, que además es la que
    /// ya usaban las plantillas del código de 2FA (<c>SeedTwoFactorTemplates</c>): los tres correos
    /// de seguridad quedan con la misma variable y la misma frase.
    ///
    /// SE EDITA CON REPLACE Y NO REESCRIBIENDO EL CUERPO ENTERO. Dos razones. La primera es que un
    /// <c>UpdateData</c> con el HTML completo obligaría a copiar aquí los dos cuerpos de las dos
    /// plantillas en dos idiomas —cuatro literales largos— y cualquier diferencia de espaciado con
    /// lo que hay en la base pasaría inadvertida. La segunda es que si alguien ha editado la
    /// plantilla desde la interfaz de AdminAPI, un REPLACE cambia SOLO la frase de la caducidad y le
    /// respeta el resto; una reescritura completa le borraría el trabajo sin avisar.
    ///
    /// Por el mismo motivo el <c>WHERE</c> filtra por <c>EventType</c> y no por los Id de semilla:
    /// alcanza cualquier localización de esas dos plantillas, incluidas las que se hayan añadido
    /// después. Las filas en otros idiomas no llevan ninguna de las dos frases y el REPLACE las deja
    /// exactamente como estaban.
    /// </remarks>
    public partial class ActualizaCaducidadEnPlantillasDeEnlace : Migration
    {
        // Fecha fija y literal: una migración tiene que producir el mismo resultado se aplique
        // cuando se aplique, así que nada de DateTime.Now/UtcNow aquí. Mismo criterio que las
        // migraciones de semilla que crearon estas filas.
        private const string FechaDeCambio = "2026-08-30T00:00:00";
        private const string Actor         = "system:seed";

        private const string FraseEnVieja = "expires in {{ExpiresInHours}} hours";
        private const string FraseEnNueva = "expires in {{ExpiresInMinutes}} minutes";
        private const string FraseEsVieja = "caduca en {{ExpiresInHours}} horas";
        private const string FraseEsNueva = "caduca en {{ExpiresInMinutes}} minutos";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(Cambio(FraseEnVieja, FraseEnNueva, FraseEsVieja, FraseEsNueva,
                                        marcador: "{{ExpiresInHours}}"));

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(Cambio(FraseEnNueva, FraseEnVieja, FraseEsNueva, FraseEsVieja,
                                        marcador: "{{ExpiresInMinutes}}"));

        /// <summary>
        /// El mismo UPDATE en las dos direcciones. El <c>marcador</c> del <c>WHERE</c> evita tocar
        /// —y marcar como modificadas— las filas que no llevan la frase de origen.
        /// </summary>
        private static string Cambio(
            string enDesde, string enHasta, string esDesde, string esHasta, string marcador) => $@"
UPDATE  l
SET     l.HtmlBody       = REPLACE(REPLACE(l.HtmlBody, N'{enDesde}', N'{enHasta}'), N'{esDesde}', N'{esHasta}'),
        l.TextBody       = REPLACE(REPLACE(l.TextBody, N'{enDesde}', N'{enHasta}'), N'{esDesde}', N'{esHasta}'),
        l.LastUpdateDate = '{FechaDeCambio}',
        l.LastUpdateBy   = N'{Actor}'
FROM    EmailTemplateLocalizations l
JOIN    EmailTemplates             t ON t.Id = l.EmailTemplateId
WHERE   t.EventType IN (N'PASSWORD_RESET', N'EMAIL_CONFIRMATION')
  AND  (l.HtmlBody LIKE N'%{marcador}%' OR l.TextBody LIKE N'%{marcador}%');";
    }
}
