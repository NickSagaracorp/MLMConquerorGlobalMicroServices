# Cómo probar la seguridad implementada

Guía paso a paso para verificar a mano todo lo que se construyó en el trabajo de
autenticación de dos factores y cierre de huecos de seguridad.

Cada prueba dice **qué comprueba**, **cómo hacerla**, **qué debe pasar** y —lo más
importante— **qué significa si falla**. Una prueba que pasa sin que se entienda qué
demostraba no sirve de nada.

---

## 0. Preparación

### 0.1 Levantar el entorno

```
docker start mlm-redis
```

Y las seis aplicaciones, cada una desde su carpeta:

| Aplicación | Puerto | Entrada |
|---|---|---|
| SignupAPI | 7005 | — |
| AdminAPI | 7002 | — |
| BizCenter | 7003 | — |
| Administración | 7001 | `https://localhost:7001/admin/login` |
| Centro de negocios | 7004 | `https://localhost:7004/login` |
| Alta | 7147 | `https://localhost:7147/ambassador-join` |

Comprobar que la caché es Redis y no el respaldo en memoria:

```
curl -sk https://localhost:7005/health/cache
```

Debe decir `"backend":"Redis"` y `"memoryFallback":false`. **Si dice lo contrario,
pare**: los límites de emisión de códigos no serán atómicos y las pruebas del
apartado 1.5 no significarán nada.

### 0.2 Dónde salen los códigos y los enlaces

Los proveedores de correo y SMS están en `Null` a propósito. Los códigos de
verificación y los enlaces de recuperación **se escriben en el log de SignupAPI**,
no se envían. Deje una ventana con el log abierta durante toda la sesión de pruebas.

Busque líneas de `NullEmailService` y `NullSmsService`.

### 0.3 Una cuenta para probar

Necesita una cuenta con 2FA activado. Actívelo desde el panel de seguridad
(`/admin/account/security` o `/account/security`) con una aplicación de
autenticación, o elija el canal de correo y lea el código en el log.

> **No use ninguna de las cuentas existentes de la base para las pruebas
> destructivas.** Varias son SuperAdmin. Cree una cuenta desechable.

---

## 1. La puerta: el segundo factor protege de verdad

### 1.1 Entrar con 2FA — administración

**Comprueba** que la contraseña sola no abre la sesión.

1. Vaya a `https://localhost:7001/admin/login`
2. Introduzca correo y contraseña correctos
3. **Antes de escribir el código**, abra otra pestaña y vaya a `https://localhost:7001/admin`

**Debe pasar:** la pantalla del código aparece con el destino enmascarado
(`p*******@dominio.com`), y el paso 3 **le devuelve al login**. Con la contraseña
dada por buena, la sesión todavía no existe.

**Si le deja entrar en el paso 3**, la contraseña está abriendo sesión por sí sola y
el segundo factor es decorativo.

### 1.2 Entrar con 2FA — centro de negocios

Lo mismo en `https://localhost:7004/login`, comprobando `https://localhost:7004/` en
el paso 3.

### 1.3 Código incorrecto

Introduzca `000000`. **Debe** volver a la pantalla del código con el aviso de código
incorrecto, **no** entrar y **no** mandarle al login como si la sesión hubiera
caducado.

### 1.4 El reto se gasta

1. Complete un acceso con su código
2. Pulse atrás hasta la pantalla del código y reenvíe el mismo código

**Debe** rechazarlo. Un código de un solo uso que se puede reutilizar no es de un
solo uso.

### 1.5 Los límites

**Emisión — 3 por ventana de 15 minutos.** Inicie sesión cuatro veces seguidas con
la contraseña correcta, sin llegar a introducir el código.

**Debe:** las tres primeras emiten reto; **la cuarta responde que se han pedido
demasiados códigos**.

**Intentos — 5 por reto.** Con un reto vivo, introduzca seis códigos incorrectos.

**Debe:** los cinco primeros dicen código incorrecto; el sexto dice **demasiados
intentos** y **quema el reto** — a partir de ahí, ni el código correcto sirve.

**Por qué importa la quema:** sin ella, el límite de cinco sería un inconveniente y
no un freno; bastaría pedir otro código y seguir probando sobre el mismo reto.

---

## 2. Los dos bypass que estaban abiertos

Estas dos son las pruebas más importantes del documento. Las dos vulnerabilidades
eran reales y permitían saltarse el segundo factor por completo.

### 2.1 El reto de login no vale como llave de la casa

**Comprueba** que el token que se emite *antes* de verificar el código no sirve para
nada más.

```bash
# 1. Iniciar sesión (sin introducir el código)
curl -sk -X POST https://localhost:7005/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"SU_CUENTA","password":"SU_CONTRASEÑA"}'
```

Copie el `challengeToken` de la respuesta y úselo como si fuera una sesión:

```bash
# 2. Intentar desactivar el 2FA con él
curl -sk -o /dev/null -w "%{http_code}\n" \
  -X POST https://localhost:7005/api/v1/auth/two-factor/disable \
  -H "Authorization: Bearer PEGUE_AQUI_EL_CHALLENGE_TOKEN"
```

**Debe responder `401`.**

Pruebe también con estos, que deben dar `401` igualmente:

```
GET  /api/v1/auth/personal-data
GET  /api/v1/auth/account-status
PUT  /api/v1/auth/change-password
POST /api/v1/auth/two-factor/channel
```

**Si alguno responde `200`**, cualquiera que consiga una contraseña filtrada tiene la
cuenta entera: desactiva el segundo factor, exporta los datos personales y cambia la
contraseña, sin tocar jamás el correo ni el teléfono de la víctima.

### 2.2 No hay una segunda puerta

**Comprueba** que no existe otro sitio donde cambiar contraseña por token.

```bash
curl -sk -o /dev/null -w "%{http_code}\n" \
  -X POST https://localhost:7002/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"SU_CUENTA","password":"SU_CONTRASEÑA"}'
```

**Debe responder `404`.** Ese endpoint existía, comprobaba la contraseña y devolvía un
token **con los roles del administrador**, sin 2FA en ninguna parte.

Y para confirmar que la puerta buena sigue viva:

```bash
curl -sk -o /dev/null -w "%{http_code}\n" \
  -X POST https://localhost:7005/api/v1/auth/login \
  -H "Content-Type: application/json" -d '{}'
```

**Debe responder `400`** (petición mal formada), no `404`.

---

## 3. La sesión

### 3.1 Se renueva sola mientras trabaja

**Comprueba** que la caducidad corta de los tokens no interrumpe a nadie.

El token de acceso dura **15 minutos**. Inicie sesión, deje el navegador abierto y
vuelva a los 20 minutos: navegue por el portal.

**Debe** seguir dentro sin pedirle nada. Cada navegación renueva la sesión por detrás.

> Para no esperar: arranque SignupAPI con `Jwt__AccessTokenExpiryMinutes=1` y repita
> con 70 segundos de espera.

### 3.2 Caduca por inactividad

El refresco dura **30 minutos de inactividad**. Inicie sesión, deje el navegador
quieto media hora larga y luego navegue.

**Debe** llevarle al login con el aviso **"Your session has expired. Please sign in
again."**, y la cookie de sesión debe haber desaparecido.

**Si le deja seguir**, el cierre por inactividad no está funcionando.

> Para no esperar: `Jwt__RefreshTokenExpiryMinutes=2` y espere 150 segundos.

### 3.3 Cambiar la postura de seguridad expulsa a quien estaba dentro

**Comprueba** que activar el 2FA no deja viva la sesión de antes.

1. Con el 2FA **desactivado**, inicie sesión y deje la sesión abierta
2. Actíve el 2FA desde el panel de seguridad
3. Intente seguir usando la sesión anterior

**Debe** echarle. El refresco emitido cuando no había segundo factor deja de valer.

Lo mismo debe ocurrir al: **desactivar** el 2FA, **confirmar** o **retirar** el
teléfono, y **cambiar el correo**.

Y **no** debe ocurrir al cambiar el canal preferido — eso es una preferencia entre
factores que ya existían, no un cambio de credenciales.

---

## 4. Suplantación

### 4.1 El modo solo lectura se aplica en el servidor

**Comprueba** que la restricción no vive solo en la pantalla.

Con una cuenta de `SupportManager`, suplante a un miembro desde administración. Copie
el token que devuelve la operación y úselo directamente contra la API:

```bash
# Una lectura — debe funcionar
curl -sk -o /dev/null -w "%{http_code}\n" \
  https://localhost:7003/api/v1/bizcenter/team/dual-tree/stats/MEMBER_ID \
  -H "Authorization: Bearer TOKEN_DE_SUPLANTACION"

# Una escritura — NO debe funcionar
curl -sk -o /dev/null -w "%{http_code}\n" \
  -X PUT https://localhost:7003/api/v1/bizcenter/profile \
  -H "Authorization: Bearer TOKEN_DE_SUPLANTACION" \
  -H "Content-Type: application/json" -d '{}'
```

**Debe:** la lectura `200`, la escritura **`403` con `IMPERSONATION_READ_ONLY`**.

**Si la escritura pasa**, el "solo lectura" es un adorno de la interfaz: quien use el
token contra la API hace lo que quiera con la cuenta ajena.

### 4.2 No se puede suplantar a personal

Intente suplantar a una cuenta que tenga un rol de panel.

**Debe** rechazarlo con `TARGET_IS_STAFF`. Antes, el token de suplantación se emitía
con los roles del suplantado — suplantar a alguien con rol de administración era una
forma de conseguir ese rol.

---

## 5. Propiedad de la cuenta

**Comprueba** que un token legítimo no sirve para operar sobre otra persona.

Con la sesión de un miembro cualquiera:

```bash
# Su propio árbol — debe funcionar
GET /api/v1/bizcenter/team/dual-tree/node/SU_MEMBER_ID          → 200

# El de un desconocido — no debe funcionar
GET /api/v1/bizcenter/team/dual-tree/node/OTRO_MEMBER_ID        → 403
```

Pruebe también, con la sesión de un miembro:

```
DELETE /api/v1/signups/membership/OTRO_MEMBER_ID     → 403
POST   /api/v1/billing/payout                        → 403
```

**Por qué importa:** aquí el token es válido y el segundo factor se pasó. Lo que se
comprueba es que estar autenticado no es lo mismo que estar autorizado sobre *ese*
sujeto. Antes, cualquier cuenta podía cancelar la membresía de otra y sacarla del
árbol binario.

---

## 6. El alta

### 6.1 Abrir el alta cierra la sesión abierta

**Comprueba** el escenario del evento: varias personas se registran desde el mismo
ordenador, una detrás de otra.

1. Inicie sesión en el centro de negocios y entre en una pantalla privada
2. **En el mismo navegador**, abra `https://localhost:7147/ambassador-join/AMB-320189`
3. Vuelva a la pantalla privada

**Debe:** el alta se ve con el patrocinador resuelto, la cookie de sesión ha
desaparecido, y el paso 3 le manda al login.

**Si la sesión sigue viva**, la persona que se registra después puede navegar a
cualquier pantalla y estar dentro de la cuenta de la anterior — su genealogía, sus
comisiones, sus datos personales.

### 6.2 El patrocinador queda guardado

Complete un alta entera por el enlace del patrocinador y compruebe en la base:

```sql
SELECT MemberId, SponsorMemberId FROM MemberProfiles WHERE MemberId = 'EL_NUEVO';
```

**Debe** tener el patrocinador correcto, no `NULL`. En pantalla el banner siempre
muestra el nombre bien; lo que hay que verificar es lo que se guarda.

### 6.3 Recuperación de contraseña

1. Pida el enlace desde `/forgot-password`
2. Búsquelo en el log de SignupAPI
3. **Lea el texto del correo**: debe anunciar **15 minutos**, no 24 horas
4. Úselo y compruebe que funciona
5. Pida otro, espere a que caduque y compruebe que ya no vale

**Debe** responder lo mismo exista o no la cuenta — si dice "esa dirección no está
registrada", el formulario sirve para averiguar qué correos tienen cuenta.

---

## 7. Lo que **no** está encendido

Esto es estado deliberado, no fallos. Si alguien lo reporta como defecto, esta es la
explicación.

| Qué | Estado | Por qué |
|---|---|---|
| `Auth:TwoFactor:MandatoryRoles` | vacío | Nadie está obligado a llevar 2FA. Encenderlo **antes** de que el correo entregue de verdad deja fuera del portal a quien tenga ese canal |
| Correo y SMS | proveedor `Null` | El log registra el evento y los nombres de las variables, nunca el código ni los enlaces. En desarrollo el correo va a MailHog (`docs/deployment/correo-en-desarrollo.md`). Encender SES es un paso de despliegue con prerrequisitos |
| Step-up fuera del cambio de cobro | sin construir | El step-up existe (Plan E) y protege solo el cambio de wallet de cobro del BizCenter. Las operaciones del Admin (§6.4 de la spec de 2FA) siguen sin él |
| Lista blanca de IP del limitador | sin declarar | El refresco tiene una red de seguridad de 600/min por IP. Los portales llaman servidor a servidor, así que en producción hay que declarar sus IP |

### Antes de encender el correo

Hay **11 cuentas con rol SuperAdmin y correo `@yopmail.com`**, que es un buzón
público sin autenticación: cualquiera que conozca la dirección lee la bandeja.
Mientras el transporte esté en `Null` es inofensivo. **En cuanto SES entregue de
verdad, cada una de esas cuentas es tomable** por quien adivine la dirección: pide
recuperación de contraseña, abre yopmail y entra.

```sql
SELECT u.Email, u.CreationDate FROM AspNetUsers u
  JOIN AspNetUserRoles ur ON ur.UserId = u.Id
  JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE r.Name = 'SuperAdmin' ORDER BY u.CreationDate;
```

Purgarlas o cambiarlas a un dominio controlado es **prerrequisito** de encender SES,
no una tarea posterior.

---

## 8. Resumen para firmar

| # | Prueba | Resultado esperado | ✔ |
|---|---|---|---|
| 1.1 | Contraseña sola no abre sesión (admin) | vuelve al login | ☐ |
| 1.2 | Contraseña sola no abre sesión (bizcenter) | vuelve al login | ☐ |
| 1.3 | Código incorrecto | aviso, no entra | ☐ |
| 1.4 | Reto reutilizado | rechazado | ☐ |
| 1.5 | Límites de emisión e intentos | corta al 4.º y al 6.º | ☐ |
| 2.1 | Reto como Bearer | `401` en los cinco endpoints | ☐ |
| 2.2 | Login de AdminAPI | `404` | ☐ |
| 3.1 | Renovación automática | sigue dentro | ☐ |
| 3.2 | Inactividad | login con aviso | ☐ |
| 3.3 | Activar 2FA expulsa | sesión anterior muerta | ☐ |
| 4.1 | Suplantación solo lectura | escritura `403` | ☐ |
| 4.2 | Suplantar a personal | `TARGET_IS_STAFF` | ☐ |
| 5 | Propiedad de la cuenta | `403` sobre terceros | ☐ |
| 6.1 | El alta cierra la sesión | cookie borrada | ☐ |
| 6.2 | Patrocinador guardado | no `NULL` | ☐ |
| 6.3 | Recuperación y su texto | 15 minutos | ☐ |

## 8. Step-up al cambiar la wallet de cobro (Plan E)

Qué protege: `PUT api/v1/bizcenter/wallets/{tipo}` y `.../{tipo}/default`. Sin un permiso de
step-up vigente y sin usar, responden `403 STEP_UP_REQUIRED` y no tocan nada. La ruta vieja
`PUT api/v1/bizcenter/wallet` ya no existe.

**Prueba en el navegador** (BizCenter, `https://localhost:7004`, MailHog en `http://localhost:8025`):

1. Mi perfil → Billing & Commissions → Payout Options → *Add account* en Volet → Guardar.
2. Se abre "Confirma que eres tú" **encima** del modal de edición, con el correo enmascarado.
3. En MailHog llega "Confirm a change to your MLM Conqueror account" con el código.
4. Un código equivocado muestra el error traducido; el bueno guarda ("Volet updated.").
5. Cancelar el código no guarda nada.

**Lo que tiene que fallar:**

| Intento | Resultado esperado |
|---|---|
| `PUT .../wallets/6/default` con token de acceso y sin `X-Step-Up-Token` | `403 STEP_UP_REQUIRED` |
| Reusar el mismo `X-Step-Up-Token` | `403 STEP_UP_INVALID` (un permiso, un uso) |
| Cambiar correo, teléfono, autenticador o canal y pedir step-up | `STEP_UP_COOLDOWN` durante 24 h, y correo de aviso al dueño |
| Más de 10 códigos de step-up fallidos en 24 h | `TWO_FACTOR_LOCKED` y correo al dueño |
| Leer el código desde el JWT del reto | imposible: `code_hash` es un HMAC con clave del servidor atado al jti |

**Condiciones de despliegue:**

- **No desplegar el BizCenter con el Plan E a producción antes de que SES (y Twilio, para quien
  use SMS) entregue de verdad.** Mientras los transportes sean los nulos, los códigos no van a
  ningún sitio y los miembros cuyo canal es correo o SMS no pueden cambiar su wallet de cobro. Se
  suma al requisito ya existente de purgar las cuentas SuperAdmin con buzón de yopmail.
- `Cache:Mode=Required` con Redis en cada host que use `[RequiresStepUp]`; sin Redis el uso
  único solo vale dentro de un proceso.
- Todos los contenedores en la **misma zona horaria** y con los **relojes sincronizados por NTP**:
  la espera compara la hora local del BizCenter (que sella el cambio de correo) con la de
  SignupAPI, y el permiso lo firma SignupAPI y lo valida el BizCenter. El validador tolera hasta
  30 s de deriva en `nbf` (permiso emitido "en el futuro" para el reloj del BizCenter) y ninguna
  en `exp`; una deriva mayor rechaza permisos recién emitidos.
- SES configurado también en el **BizCenter**: es quien avisa al correo anterior cuando cambia
  el correo.
- Al desplegar, los retos emitidos en los 5 minutos anteriores dejan de valer (cambió la huella
  del código): basta pedir uno nuevo.
- Producto: un miembro que confirma su primer teléfono o autenticador espera 24 h antes de
  poder elegir su wallet de cobro.

## 9. Compra de tokens (Plan F)

Qué protege: `api/v1/billing/tokens/*` (Billing) y sus pasos por el BizCenter. Las pruebas en el
navegador y con `curl` las completa la Tarea 16 del plan F
(`docs/superpowers/plans/2026-10-15-plan-f-compra-de-tokens.md`); aquí quedan ya las condiciones
que no pueden esperar a esa tarea.

**Condiciones de despliegue:**

- **BLOQUEANTE — la tarjeta no se enciende en producción.** `Billing:TokenPurchase:CardEnabled`
  tiene que valer `false` (es el valor de `appsettings.json`) mientras `SpreedlyCardGatewayService`
  sea un stub: hoy devuelve un éxito simulado (`simulated-spreedly-txn-…`) sin cobrar. Con la
  tarjeta apagada la pantalla no muestra la pestaña y Billing rechaza la compra con
  `400 CARD_PAYMENTS_DISABLED` antes de reservar nada. Si alguien la enciende por error, Billing no
  emite por un id simulado (la compra queda a conciliar con `CARD_SIMULATED_CHARGE` y sale un
  `Critical` en el log), pero el miembro no paga ni recibe nada: no es un estado aceptable.
  Encenderla exige el cobro real con Spreedly, sus rechazos explícitos añadidos a
  `CardChargeOutcomes.ExplicitNoChargeCodes` y una compra de prueba real en sandbox (plan F, R13 y
  Tarea 16 paso 9).
- **El barrido de compras atascadas tiene que estar vivo.** En `/hangfire` de Billing debe existir
  el trabajo recurrente `token-purchase-stale-pending` (cada hora) y
  `Billing:TokenPurchase:StalePendingMinutes` no puede ser `0`. Es lo que pasa a conciliar una
  compra con tarjeta o eWallet que se quedó en `Pending` porque el proceso o la base cayeron entre
  el cobro y la emisión (R5). Nunca libera el cupo: tesorería mira el gateway y la cierra en
  *Admin → Billing → Token Purchases → To review*.
- **Altas en cripto: las direcciones antes que SignupAPI.** El alta en cripto solo se ofrece y
  SignupAPI solo la acepta si hay `Crypto.DepositAddress.{BTC|ETH|USDT}` en *Admin → Settings →
  Global Parameters* (la red, `Crypto.DepositNetwork.*`, es opcional, pero para USDT conviene
  ponerla); sin ellas, `400 CRYPTO_ADDRESS_NOT_CONFIGURED`. Configurarlas **antes** de desplegar
  la versión nueva de SignupAPI.
- **`Services:BillingBaseUrl` definida en BizCenter y en AdminAPI** para cada entorno (variable
  `Services__BillingBaseUrl` o `appsettings.{Entorno}.json`). No está en el `appsettings.json` base
  a propósito: fuera de Development, sin ella el arranque falla en vez de mandar el token del
  miembro o del administrador a localhost. AdminAPI ya no lee `Services:Billing`.
- **Revisar el log de Billing por `Critical`** de la compra de tokens tras el despliegue: cada uno
  lleva el id de una compra cuyo resultado no se pudo guardar y hay que mirar a mano.
- El orquestador de tarjeta pasa al siguiente gateway ante cualquier fallo (R5b del plan): la compra
  de tokens lo evita llamando un paso cada vez, pero el cobro recurrente no. Mientras la tarjeta
  sea un stub no hay dinero en juego; antes de encender el cobro real hay que corregirlo en el
  orquestador.

| # | Prueba | Resultado esperado | ✔ |
|---|---|---|---|
| 9.1 | `POST api/v1/billing/tokens/purchase` con `"paymentMethod":"CreditCard"` y la tarjeta apagada | `400 CARD_PAYMENTS_DISABLED`, sin pedido ni compra | ✔ (2026-10-15, por API) |
| 9.2 | Catálogo con la tarjeta apagada | `cardEnabled: false` y sin pestaña de tarjeta | ✔ (2026-10-15, por API) |
| 9.3 | Misma clave de idempotencia con otra cantidad | `409 IDEMPOTENCY_KEY_REUSED` | ✔ (2026-10-15, por API) |
| 9.4 | `token-purchase-stale-pending` en `/hangfire` | existe, cada hora | ☐ |
| 9.5 | Ficha de un miembro en AdminWeb → pestaña Tokens, con un usuario SupportLevel1 | los códigos salen como `ABC****` (también dentro de las notas); con Admin, enteros | ☐ |
| 9.6 | Confirmar una cripto o conciliar una compra desde AdminWeb, mirando la respuesta en las herramientas del navegador | `tokenCodes: []` | ☐ |
| 9.7 | *Reopen & confirm* sobre una compra cancelada o rechazada (por API) | `409 TOKEN_PURCHASE_NOT_EXPIRED`, sin emitir | ☐ |
| 9.8 | Dos compras cripto simultáneas que juntas pasan el cupo | una 200 y otra `409 TOKEN_MONTHLY_LIMIT_REACHED` | ✔ (2026-10-15, por API) |
| 9.9 | Pagar con comisiones sin step-up, también llamando a Billing directo con `" EWallet "` | `403 STEP_UP_REQUIRED` | ✔ (2026-10-15, por API) |
