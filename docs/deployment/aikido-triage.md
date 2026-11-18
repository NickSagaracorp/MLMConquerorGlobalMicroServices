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
