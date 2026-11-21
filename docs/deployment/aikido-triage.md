# Triage de las alertas de Aikido (repo MLMConquerorGlobalMicroServices)

Estado al 17-nov-2026, rama `correcciones-aikido`. Cada alerta queda como **corregida** (con su
commit) o como **riesgo aceptado / falso positivo** (con la justificación que hay que pegar en
Aikido al marcarla).

## Corregidas

| Alerta | Qué se hizo |
|---|---|
| Critical, Unlicensed: `itext7` / `itext7.bouncy-castle-adapter` 9.1.0 (Billing, RankEngine) | iText7 (AGPL) se sustituyó por **PDFsharp 6.2.4** (MIT) en el proyecto común `MLMConquerorGlobalEdition.Pdf`. **No subir a PDFsharp 6.5.0 ni a 7.0.0-preview-2: esas versiones pasaron a AGPL-3.0.** Las fuentes incrustadas son Liberation (SIL OFL 1.1). |
| High: `Microsoft.AspNetCore.Components.WebAssembly.Server` (XSS) | Ya estaba corregida en `10c525d` (10.0.5 → 10.0.13). |
| High: `document.write` en `custom.js`, `dragdrop.js` y otros | Eran scripts de la plantilla que ninguna página carga pero que AdminWeb y BizCenterWeb servían como estáticos. Se borraron de los dos portales. |
| High: open redirect en `AccountEndpoints.cs` | Las redirecciones internas del área de cuenta y de la puerta usan `Results.LocalRedirect`, que rechaza un destino fuera del sitio. La única salida externa es el `returnUrl` del cierre de sesión, validado contra `SignOut:AllowedReturnUrls`. |
| High: unsigned JWT en las MAUI (`AdminJwtAuthStateProvider`, `AdminViewContextInitializer`, `JwtAuthStateProvider`, `ViewContextInitializer`) | 18-nov-2026. Ya no se lee el token sin validar. SignupAPI publica su llave pública en `/.well-known/jwks.json` (`JwksDocument`); las apps la descargan, la guardan en Preferences y validan firma RS256, emisor, audiencia y vigencia con `ClientAccessTokenValidator` (ClientCore) antes de armar la identidad. Un reto de 2FA no pasa. Si la API rota la llave, la app la vuelve a descargar sola (como mucho una vez por minuto), sin publicar otra versión en las tiendas. Los portales usan la misma comprobación (`SignedAccessToken`) en `SessionExpiry` y en la pantalla del reto (`TwoFactorPageData`): ya no queda ningún `ReadJwtToken` en el código. |
| High: unsigned JWT (portales web) | AdminWeb y BizCenterWeb validan firma, emisor, audiencia y vigencia del token de acceso (`PortalAccessTokenValidator`) antes de montar la cookie de sesión. Necesitan `Jwt:PublicKeyBase64`, `Jwt:Issuer` y `Jwt:Audience` en su configuración (iguales que en SignupAPI) y no arrancan sin ellos. |

## Riesgo aceptado / falso positivo

### Critical, Unlicensed: `Hangfire.AspNetCore` / `Hangfire.Core` 1.8.x
Falso positivo de metadatos. El paquete declara su licencia como archivo (`<license type="file">`)
en vez de con una expresión SPDX, y el escáner lo muestra como "sin licencia". Hangfire es
**LGPL-3.0** (con licencia comercial opcional); se usa como biblioteca sin modificarla, que es
exactamente lo que la LGPL permite. Justificación para Aikido: *"Hangfire is LGPL-3.0 (see
LICENSE.md in the package); used unmodified as a dynamically linked library. The package declares
its license as a file, not an SPDX expression."*

### Carpeta `UI-Templates/`
Es la plantilla HTML de referencia del diseño. No se compila ni se despliega; sus scripts
(`custom.js`, `dragdrop.js`, echarts…) seguirán apareciendo en el escáner. Se puede excluir la
ruta `UI-Templates/` en Aikido.

## Segunda tanda (17-nov-2026)

| Alerta | Resultado |
|---|---|
| Critical: `BouncyCastle.Cryptography` (missing input validation) | **Ya resuelta.** Entraba solo como dependencia de `itext7.bouncy-castle-adapter`, que se quitó con iText7 en la primera tanda; ningún proyecto la referencia ahora. Si sigue apareciendo, el escaneo es anterior a ese commit. |
| High: "Rendering unescaped input can lead to XSS" en `AdminClubRecognitionReportController`, `AdminRankStandingsReportController` (y las otras exportaciones) | **Falso positivo.** Es la exportación a CSV: `File(bytes, "text/csv", nombre)` sale como descarga (`Content-Disposition: attachment`), el AdminAPI manda `X-Content-Type-Options: nosniff` en todas las respuestas, y `CsvReportWriter` neutraliza las fórmulas (`=`, `+`, `-`, `@`, tabulador, retorno). Nada se interpreta como HTML. Justificación: *"CSV file download (attachment, text/csv, nosniff); not rendered as HTML. Formula injection is neutralized in CsvReportWriter."* |
| High: SQL injection por concatenación en `MemberNameSearch.cs`, `LegPointMaintenance.cs` y otros 3 | **Falso positivo, con endurecimiento.** Lo concatenado son fragmentos fijos de SQL y nombres de parámetros; todo valor que llega del usuario va como `SqlParameter`. Se añadió validación del prefijo de parámetros en `MemberNameSearch.MatchingIdsSql` (solo `@identificador`) y pruebas de que el término nunca entra en el texto del SQL. Justificación: *"Only constant SQL fragments and parameter names are concatenated; all user values are bound as SqlParameter."* |
| Medium: authorization bypass en `EntraxSignupsController.cs` (SignupAPI) | **Corregida.** El controlador queda cerrado por defecto con `[Authorize]` a nivel de clase; las acciones que tienen que ser anónimas (opciones de pago y estado del QR durante el alta, y el callback de Entrax, que se autentica por firma HMAC) lo declaran con `[AllowAnonymous]`. |
| Medium: SSRF en `AdminEntraxSignupsController.cs` | **Endurecida.** La petición va siempre a la dirección base fija del cliente de SignupAPI, y ahora la ruta se arma con el Id del pago leído de la base, no con el de la URL. |

## Tercera tanda (20-nov-2026, escaneo del repositorio del cliente)

| Alerta | Resultado |
|---|---|
| High: path traversal en `AdminUploadAttachmentHandler.cs` | **Corregida.** `AdjuntoEnDisco` (SharedKernel) valida la extensión contra la lista también en el manejador, el nombre en disco es un Guid y la ruta final se comprueba contra la carpeta antes de escribir. El manejador del centro de negocios usa lo mismo. |
| Medium: open redirect en `SessionExpiry.cs` | **Corregida.** La salida por sesión caducada solo redirige a una ruta local (`EsRutaLocal`, mismas reglas que `Url.IsLocalUrl`); si la configuración trae otra cosa, va a `/`. |
| Medium: authorization bypass en `ExternalController` y `WebhooksController` (SharedAPICenter) | **Corregida.** SharedAPICenter tiene autenticación propia, sin JWT: `ApiKeyExterna` (X-Api-Key en tiempo constante) y `FirmaWebhook` (HMAC-SHA256 de `timestamp.cuerpo` con `Webhooks:{provider}:Secreto`, ventana de 5 minutos). Las dos fallan cerradas. Cierra también el bloqueante de los webhooks sin firma. |
| Medium: authorization bypass en `SignupsController` (SignupAPI) | **Corregida.** `[Authorize]` de clase; los pasos del alta y sus consultas llevan `[AllowAnonymous]`. |
| High: improper SSL certificate validation y Medium: android backup (AdminApp, BizCenterApp) | **Corregidas.** `allowBackup=false`, `dataExtractionRules` que excluyen todo, `usesCleartextTraffic=false` y `network_security_config` con solo HTTPS y solo las autoridades del sistema. |
| Medium: XSS por `MarkupString` (Signup, MemberJoin, TicketDetailPage, AdminProducts, Error) | **Corregidas.** El cuerpo del ticket (texto del miembro) se pinta como texto. Las descripciones de productos y concursos pasan por `HtmlSaneado`, que limpia con `HtmlSeguro`, la misma política con la que AdminAPI limpia al guardar. La tabla de productos y la página de error pintan texto plano. Queda un único `MarkupString`, el de `HtmlSaneado`, sobre HTML ya saneado. Justificación: *"Only rendered after allowlist sanitization (Ganss.Xss) in HtmlSaneado; same policy applied on write."* |
| Medium: `Azure.Core` (AIKIDO-2026-895105) | **Corregida.** Fijado `Azure.Core` 1.64.0 en Repository (entraba 1.50.0 por Microsoft.Data.SqlClient). |
| Medium: `System.IdentityModel.Tokens.Jwt` (AIKIDO-2026-11098) | **Corregida.** 8.9.0 → 8.22.0 en ClientCore, SharedComponents.Server, AdminApp y BizCenterApp (el resto ya estaba en 8.22.0). |
| Low: insecure cookie (`AuthEndpoints.cs`, `AdminWeb/Program.cs`, `BizCenterWeb/Program.cs`) | **Corregida.** La cookie del idioma sale de `CookieDeIdioma`: `Secure`, `HttpOnly`, `SameSite=Lax`. Se quitó el script del AdminWeb que la leía (el `lang` ya lo pone el servidor). |
| Low: SSRF en `AdminEntraxSignupsController.cs` | **Endurecida.** `DestinoDelRecheck` arma la URL absoluta sobre la dirección base del cliente de SignupAPI y no sale si queda en otro esquema, host o puerto. |
| High/Medium: `document.write`/`innerHTML` en `UI-Templates/` | **Resuelta por entrega.** `UI-Templates/` deja de viajar al cliente (decisión de Nick, 20-nov-2026): son plantillas de referencia que no se despliegan. |

## Cuarta tanda (20-nov-2026, reescaneo del repositorio del cliente tras la entrega e76b002)

| Alerta | Resultado |
|---|---|
| Medium: XSS por `MarkupString` en `HtmlSaneado.razor` | **Corregida.** `HtmlSaneado` pasa a ser una clase C# (`ComponentBase`) que entrega al árbol de render, con `AddMarkupContent`, solo el texto que devolvió `HtmlSeguro.Limpiar`. Ya no hay ningún `MarkupString` en la solución. Pruebas con bUnit (`HtmlSaneadoTests`): quita `<script>`, `onerror` y `javascript:`, y vuelve a limpiar cuando cambia el parámetro. |
| Low (rebajadas por Aikido): SSRF en `AdminEntraxSignupsController.cs` líneas 152 y 178 | **Corregida.** El Re-check solo acepta un Id que sea un Guid (el Id de `EntraxPayment` siempre lo es); cualquier otra cosa responde 404 sin tocar la base ni la red. `DestinoDelRecheck` recibe un `Guid` y la URL es la dirección base fija más una ruta constante cuyo único tramo variable es ese Guid, que no puede traer barras, puntos ni otro host. |

## Reescaneo del 21-nov-2026 (repositorio del cliente, entrega 7e218b4)

Quedan 2 alertas abiertas; las dos se ignoran en Aikido con justificación (61 resueltas el 20-nov).

| Alerta | Resultado |
|---|---|
| Critical, Unlicensed: `Hangfire.Core`, `Hangfire.AspNetCore`, `Hangfire.NetCore` 1.8.19 (SignupAPI, RankEngine, CommissionEngine, BizCenter) | **Falso positivo de metadatos, se ignora.** El paquete declara la licencia como archivo (`<license type="file">LICENSE.md</license>`) y no como expresión SPDX; la 1.8.23 lo declara igual, así que actualizar no la quita. Hangfire es LGPL-3.0 y se usa sin modificar. Justificación: *"Hangfire is LGPL-3.0 (see LICENSE.md in the package); used unmodified as a dynamically linked library. The package declares its license as a file, not an SPDX expression."* |
| Low (rebajada por Aikido): SSRF en `AdminEntraxSignupsController.cs` línea 143 | **Se ignora.** El escáner marca la dirección base del cliente HTTP hacia SignupAPI, que viene de la configuración del servidor: en el escaneo anterior marcó también `new Uri(baseAddress, "api/v1/signups/entrax/")`, sin ningún dato variable. El Id solo se acepta si es un Guid (si no, 404) y es el único tramo variable de una ruta constante. Justificación: *"Destination is the fixed SignupAPI base address from server configuration plus a constant path; the only variable segment is a parsed Guid, so the request cannot be redirected to another host or path."* |
