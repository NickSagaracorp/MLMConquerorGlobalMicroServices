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

### 6.4 Ningún camino activa ni sube de nivel sin cobrar

Qué protege: `POST api/v1/signups/{signupId}/complete` (anónimo) con `"paymentMethod":"CreditCard"`.
En el alta nada cobra una tarjeta: la tokenización del navegador es simulada
(`SimulatedCardTokenizationService`) y `SpreedlyCardGatewayService` es un stub. Con la tarjeta
encendida, `CompleteSignup` guarda la tarjeta y **activa la membresía y genera comisiones sin cobrar**.

**Condiciones de despliegue:**

- **BLOQUEANTE — la tarjeta del alta no se enciende en producción.** `Signup:CardEnabled` tiene que
  valer `false` en SignupAPI (es el valor de `appsettings.json`; si falta la clave, o no es `true`,
  también vale apagada) **hasta que exista el cobro real con 3DS**. Apagada, `payment-options` trae
  `cardEnabled: false`, las dos páginas del alta (`Signup.razor` y `MemberJoin.razor`) no muestran la
  pestaña y `complete` con tarjeta responde `400 CARD_PAYMENTS_DISABLED` antes de leer el pedido: no
  activa, no guarda la tarjeta, no sube la captura y el pedido sigue en `Pending` para pagar con otro
  método. El servidor manda: aunque una pantalla vieja muestre la pestaña, la rechaza.
- Encenderla exige el cobro real (tokenización real en el navegador, cargo con 3DS por Spreedly y
  activación solo con el cargo confirmado) y una alta de prueba real en sandbox. Es lo mismo que
  `Billing:TokenPurchase:CardEnabled` en la compra de tokens (§9).

| # | Prueba | Resultado esperado | ✔ |
|---|---|---|---|
| 6.4.1 | `POST api/v1/signups/{signupId}/complete` con `"paymentMethod":"CreditCard"` y un cuerpo `creditCard` válido, con la tarjeta apagada | `400 CARD_PAYMENTS_DISABLED`, sin activar: pedido en `Pending`, miembro y suscripción en `Pending`, usuario inactivo, ninguna fila en `CreditCards` ni comisión; después el mismo alta se completa con otro método. **Sin** cuerpo `creditCard` la respuesta es el `400` de validación del modelo (no llega al handler), tampoco activa | ☐ |
| 6.4.2 | `GET api/v1/signups/payment-options` con la tarjeta apagada | `cardEnabled: false` | ☐ |
| 6.4.3 | Abrir `/ambassador-join/…` y `/member-join/…` con la tarjeta apagada | sin pestaña *Credit Card*; elegida por defecto *Token*, también al cambiar de país y al volver de un QR de Entrax | ☐ |
| 6.4.4 | `POST api/v1/admin/signups/{orderId}/retry-complete` (AdminAPI), también desde el botón *Retry Complete Signup* de *Members* | `410 SIGNUP_RETRY_RETIRED` sin llamar a SignupAPI ni tocar nada | ☐ |
| 6.4.7 | `complete` con un método fuera de la lista (`"paymentMethod": 99`) | `400`: la validación del modelo lo rechaza; si llegara al handler, `400 PAYMENT_METHOD_NOT_SUPPORTED`. Nunca activa | ☐ |

**Métodos admitidos.** `CompleteSignup` decide con una **lista de admitidos**, antes de leer el pedido:
`Token`, `Crypto` y `Entrax` pasan; `CreditCard` solo con `Signup:CardEnabled`; `DiscountCode` es
`DISCOUNT_CODES_DISABLED`, y cualquier otro valor `PAYMENT_METHOD_NOT_SUPPORTED`. Un método nuevo no
puede activar por caer en la rama que activa.

**`retry-complete` retirado.** Existía para "reintentar" altas zombis reenviando `complete` con
`paymentMethod: 1` (tarjeta) **sin tarjeta**, es decir, para activarlas sin cobrar. La validación de
SignupAPI ya lo rechazaba siempre, y ningún método que cobra se puede repetir así (el token pide su
código, la cripto su moneda, Entrax su QR). Ahora responde `410 SIGNUP_RETRY_RETIRED`. Un alta a medias se
resuelve por su camino: la cripto se confirma en *Crypto Signups*, Entrax con *Re-check*, y lo demás con
la activación manual (abajo).

**Códigos de descuento.** No existe ningún sistema de códigos de descuento (ni entidad, ni validación, ni
pantalla que los use). Antes, `complete` con `"paymentMethod":"DiscountCode"` (o `4`) y cualquier cadena
de 4 a 32 caracteres (`FREE100`) activaba la membresía y generaba comisiones gratis en un endpoint
anónimo. Ahora se rechaza **siempre**, antes de leer el pedido. No hay interruptor: no hay nada que
encender hasta que exista un sistema de códigos real. `Scripts/SignupLoadTest` completaba la fase 3 con
un código de descuento y ya no puede activar altas.

**Subida de nivel del miembro.** `POST api/v1/members/{memberId}/membership/upgrade` (SignupAPI, del
propio miembro o de personal) cambia de nivel **sin cobrar nada**. Su único llamador es ese endpoint;
AdminAPI no tiene subida de nivel (`AdminMemberMembershipController` solo lee).

**Condición de despliegue:**

- **BLOQUEANTE — la subida de nivel no se enciende en producción.** `Membership:UpgradeEnabled` tiene
  que valer `false` (es el valor de `appsettings.json`; si falta o no es `true`, también apagada) hasta
  que la subida cobre la diferencia (Plan G). Apagada, responde `400 UPGRADE_DISABLED` sin cambiar nada.

| # | Prueba | Resultado esperado | ✔ |
|---|---|---|---|
| 6.4.5 | `complete` con `"paymentMethod":"DiscountCode"` y `"discountCode":"FREE100"` sobre un alta en `Pending` (también con la tarjeta encendida) | `400 DISCOUNT_CODES_DISABLED`, sin activar: pedido, miembro y suscripción en `Pending`, usuario inactivo, sin comisiones; el alta se completa después con otro método | ☐ |
| 6.4.6 | `POST api/v1/members/{suMemberId}/membership/upgrade` con un nivel superior y la subida apagada | `400 UPGRADE_DISABLED`; la suscripción activa no cambia y no aparece otra | ☐ |

**Activación manual del personal.** `PUT api/v1/admin/members/{memberId}/status` (AdminAPI) con
`"status":"Active"` es una **activación manual deliberada**: pasa la suscripción `Pending` a `Active` y el
pedido `Pending` a `Completed` **sin cobro** (para un pago verificado por fuera). No calcula comisiones ni
activa el usuario de acceso. Pueden hacerlo `SuperAdmin`, `Admin`, `SupportManager` y `SupportLevel1` a
`SupportLevel3` (los roles de `MembersController`). Queda auditada en `MemberStatusHistory` (estado
anterior y nuevo, motivo, quién —`CreatedBy`— y cuándo). Lo que **no** puede es pisar un pago en vuelo:
si el pedido del alta (el primero del miembro) está en `Processing` —cripto o Entrax esperando— o hay un
`EntraxPayment` en `AwaitingPayment`, responde `409 PAYMENT_IN_FLIGHT` sin cambiar nada (ni historial);
ahí lo correcto es confirmar la cripto o hacer *Re-check*.

| # | Prueba | Resultado esperado | ✔ |
|---|---|---|---|
| 6.4.8 | Activar a mano un miembro con su alta en cripto pendiente de confirmar (pedido `Processing`) | `409 PAYMENT_IN_FLIGHT`; miembro, suscripción y pedido sin cambios, sin fila en `MemberStatusHistory` | ☐ |
| 6.4.9 | Activar a mano un miembro con un QR de Entrax en `AwaitingPayment` | `409 PAYMENT_IN_FLIGHT`, sin cambios | ☐ |
| 6.4.10 | Activar a mano un miembro `Pending` sin pago en vuelo, con motivo | `200`; activo, suscripción `Active`, pedido `Completed` y una fila en `MemberStatusHistory` con el usuario y el motivo | ☐ |

---

## 7. Lo que **no** está encendido

Esto es estado deliberado, no fallos. Si alguien lo reporta como defecto, esta es la
explicación.

| Qué | Estado | Por qué |
|---|---|---|
| `Auth:TwoFactor:MandatoryRoles` | vacío | Nadie está obligado a llevar 2FA. Encenderlo **antes** de que el correo entregue de verdad deja fuera del portal a quien tenga ese canal |
| Correo y SMS | proveedor `Null` | El log registra el evento y los nombres de las variables, nunca el código ni los enlaces. En desarrollo el correo va a MailHog (`docs/deployment/correo-en-desarrollo.md`). Encender SES es un paso de despliegue con prerrequisitos |
| Step-up fuera del cambio de cobro | sin construir | El step-up existe (Plan E) y protege solo el cambio de wallet de cobro del BizCenter. Las operaciones del Admin (§6.4 de la spec de 2FA) siguen sin él |
| `Membership:UpgradeEnabled` (subida de nivel del miembro) | `false` | La subida cambia de nivel sin cobrar. Bloqueante hasta que cobre la diferencia, Plan G (§6.4) |
| Códigos de descuento en el alta | sin sistema | `DiscountCode` se rechaza siempre con `DISCOUNT_CODES_DISABLED`: no hay códigos que validar (§6.4) |
| `Signup:CardEnabled` (tarjeta en el alta) | `false` | Nada cobra la tarjeta del alta (tokenización simulada, gateway stub): encendida activaría y comisionaría sin dinero. Bloqueante hasta el cobro real con 3DS (§6.4) |
| Activación manual del personal | encendida, limitada | `PUT admin/members/{id}/status` a `Active` activa sin cobro a propósito (pago verificado por fuera), auditada en `MemberStatusHistory`; no pisa un pago en vuelo (`409 PAYMENT_IN_FLIGHT`). La pueden hacer también `SupportLevel1` a `SupportLevel3` (§6.4) |
| `retry-complete` de AdminAPI | retirado | Activaba altas sin cobrar; `410 SIGNUP_RETRY_RETIRED` (§6.4) |
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

## 10. Entrax en el alta: rublos por SBP (Plan H)

Qué protege: el alta de un aspirante de un país de `Entrax:Countries` (por defecto `RU`), que puede
pagarse con un QR de SBP de Entrax. **El alta solo la activa el servidor**, por un único camino
(`EntraxSignupSettlement`) al que llegan el callback de Entrax, la pantalla que consulta el estado,
el barrido `entrax-signup-sweep` y el *Re-check* del admin. Ese camino **siempre** vuelve a preguntar
`GET /payment/{id}` a Entrax: el cuerpo del callback solo dice qué pago mirar, no decide nada. Exige
`paid`, moneda `USD` e importe pagado ≥ lo exigido (el mayor entre lo pedido en el QR y el
`Orders.TotalAmount` actual); si algo no cuadra, el pago queda `ToReview` sin activar nada y con un
`Critical` en el log. Plan: `docs/superpowers/plans/2026-10-16-plan-h-entrax-en-el-alta.md`.

Endpoints (SignupAPI, `https://localhost:7005` en Development, salvo los de AdminAPI):

| Endpoint | Acceso | Qué hace |
|---|---|---|
| `GET api/v1/signups/payment-options` | anónimo | países donde se ofrece Entrax (vacío si `Entrax:Enabled = false`) |
| `POST api/v1/signups/{signupId}/complete` con `"paymentMethod": 5` | anónimo | reserva el pedido (`Processing`), guarda el intento y pide el QR |
| `GET api/v1/signups/{signupId}/entrax-status?ref={paymentRef}` | anónimo, 30/min por IP | estado, `expiresAt` y `secondsRemaining`; nada más |
| `POST api/v1/signups/entrax/callback` | anónimo, solo actúa con firma válida | el aviso de Entrax |
| `POST api/v1/signups/entrax/{entraxPaymentId}/recheck` | `CryptoPaymentApprovers` | vuelve a preguntar a Entrax y liquida |
| `GET api/v1/admin/entrax-signups?view=open` (o `review`, `recent`) — AdminAPI | `CryptoPaymentApprovers` | listas del admin, paginadas |
| `POST api/v1/admin/entrax-signups/{id}/recheck` — AdminAPI | `CryptoPaymentApprovers` | reenvía a SignupAPI con el Bearer del administrador, tope de 20 s, auditado |
| `POST api/v1/dev/entrax/{paymentId}/{outcome}` (`paid`, `underpaid`, `failed`, `expired`) y `POST api/v1/dev/entrax/sweep` | solo Development **y** `Entrax:UseFake` | manejan el fake y fuerzan un barrido |

`CryptoPaymentApprovers` son `Admin`, `SuperAdmin`, `SupportLevel3` y `BillingManager`, los mismos
que confirman una cripto. Se comprueban en AdminAPI y otra vez en SignupAPI.

**Condiciones de despliegue:**

- **Credenciales y callback por variables de entorno, nunca en `appsettings`.** `Entrax__KeyId`,
  `Entrax__Secret` y `Entrax__CallbackUrl` (o el almacén de secretos del entorno). El
  `appsettings.json` base lleva `CallbackUrl` vacía y ni `KeyId` ni `Secret` (lo vigila
  `EntraxConfiguracionTests`). Fuera de Development, `Entrax:Enabled = true` sin `KeyId`, sin
  `Secret`, sin una `CallbackUrl` https absoluta o con una `BaseUrl` sin https **no arranca**
  (`EntraxOptionsGuard`).
- **URL pública HTTPS del callback, alcanzable desde Entrax**:
  `https://{host-público-de-SignupAPI}/api/v1/signups/entrax/callback`. Comprobar desde fuera de la
  red (no desde el propio servidor) que un `POST` sin firma recibe `401`: eso demuestra que llega.
- **Apagar Entrax es `Entrax:Enabled = false`, no quitar credenciales** (H19). El barrido corre
  aunque esté apagado y liquida los pagos en vuelo con esas credenciales. Fuera de Development, unas
  credenciales **a medias** (solo `KeyId` o solo `Secret`) o una `BaseUrl` sin https **no arrancan**
  aunque esté apagado. Sin ninguna credencial el cliente no llama (`NOT_CONFIGURED`): el barrido
  cierra esos QR en local como `Expired` (`LOCAL_EXPIRY`) pasado `ExpiresAt` +
  `Entrax:LocalExpiryGraceMinutes` y el pedido vuelve a `Pending`, pero un pago real ya no se puede
  comprobar hasta que vuelvan. **Con Entrax apagado y pagos esperando, el barrido escribe un
  `Critical`** en cada pasada.
- **`Entrax:UseFake` nunca fuera de Development**: si está puesto, SignupAPI no arranca, esté o no
  encendido.
- **El barrido registrado y en marcha.** En `/hangfire` de SignupAPI debe existir el recurrente
  `entrax-signup-sweep` (cada 2 minutos, cola `signups`) y tiene que haber un servidor de Hangfire
  que atienda esa cola. Es lo que activa las altas cuyo callback no llegó o se rechazó y lo que cierra
  los QR caducados.
- **IP de Entrax en `IpRateLimiting:IpWhitelist`** de SignupAPI (R13), si Entrax las publica. Si no,
  la regla general (60/min por IP) puede cortar callbacks cuando haya muchas altas: Entrax reintenta y
  el barrido liquida, pero con retraso.
- **`Services__SignupAPI` definida en AdminAPI fuera de Development.** No está en el `appsettings.json`
  base a propósito: por ese cliente (`SignupApi`) viaja el Bearer del administrador. **Sin ella AdminAPI
  no arranca** (no solo falla el Re-check: no arranca el servicio entero). Ese mismo cliente lo usan las
  pantallas de altas (reintentar un alta) y de altas en cripto (confirmar una cripto). Fuera de
  Development valida el certificado de SignupAPI: tiene que ser uno de confianza.
- **Migración `20261016045914_EntraxEnElAlta` aplicada antes de que la versión nueva de SignupAPI
  atienda tráfico.** Los servicios ejecutan `MigrateAsync()` al arrancar, así que la aplica el primero
  que se despliegue; si en ese entorno las migraciones se aplican a mano, hacerlo antes de desplegar
  SignupAPI (y AdminAPI, que lee `EntraxPayments`). Crea `EntraxPayments` y `EntraxApiCalls` con los
  índices únicos `IX_EntraxPayments_ExternalOrderId`, `IX_EntraxPayments_ExternalPaymentId` y
  `UX_EntraxPayments_OrderId_Consumed`, que son parte de la protección (sin ellos, un pago podría
  consumir dos altas, o dos pagos un alta).
- **Hora del servidor sincronizada por NTP.** El callback se rechaza (`400 STALE_CALLBACK`) si su
  `timestamp` se aleja más de `Entrax:CallbackToleranceMinutes` (60; nunca menos de 40) de la hora UTC
  del servidor, hacia atrás o hacia delante. Un reloj desviado rechaza callbacks buenos (el barrido
  liquidaría igual, con retraso).
- **No hay sandbox documentado de PaymentQr.** La primera prueba real se hace con **un importe mínimo y
  con la aprobación de Nick**, con un alta de prueba de RU, y en este orden: (1) el QR sale **en USD**
  por el total del pedido (si el comercio no acepta USD, `502 ENTRAX_UNAVAILABLE`: pedir a Entrax que
  lo habilite; **no** convertir nosotros); (2) pagarlo de verdad y comprobar que llega el callback,
  que **su firma valida** (si se rechaza con `401`, revisar con Entrax qué firman: se acepta el HMAC
  del cuerpo crudo en Base64 o en hexadecimal) y que el alta queda activa con un `PaymentHistory`
  `Entrax`; (3) dejar caducar otro QR sin pagarlo y comprobar que la pantalla ofrece uno nuevo. Hasta
  entonces no se anuncia el método.
- **Revisar el log de SignupAPI por `Critical` de Entrax** tras el despliegue: cada uno es un pago que
  quedó `ToReview` (importe, moneda, doble cobro, discrepancia del proveedor) o Entrax apagado con pagos
  esperando. Los pagos se resuelven en *Admin → Billing → Entrax Signups → To review*; la devolución se
  hace a mano con Entrax (la API v1 no documenta reembolsos).

**Preparación de las pruebas** (Development, con el fake): arrancar SignupAPI con
`Entrax__Enabled=true`, `Entrax__UseFake=true` y `Entrax__Secret=secreto-dev`. Para firmar un
callback a mano (PowerShell; `body.json` sin BOM; `$paymentId` es `EntraxPayments.ExternalPaymentId`
y `$externalOrderId` es `EntraxPayments.ExternalOrderId`):

```powershell
$secret = "secreto-dev"
$ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$body = '{"paymentId":"' + $paymentId + '","orderId":"' + $externalOrderId + '","status":"SUCCESS","timestamp":' + $ts + '}'
[IO.File]::WriteAllText("$PWD\body.json", $body)
$hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$sig = [Convert]::ToBase64String($hmac.ComputeHash([IO.File]::ReadAllBytes("$PWD\body.json")))
curl.exe -k -i -X POST https://localhost:7005/api/v1/signups/entrax/callback -H "Content-Type: application/json" -H "X-Signature: $sig" --data-binary "@body.json"
```

La Tarea 22 del plan las pasó el 2026-10-16 de punta a punta contra el fake, por API (sin
navegador) y con los servicios en `7b1cc25`. ✔ = verificada; ✔ en parte = lo que dice la nota;
✘ = falla; ☐ = pendiente, con el motivo. Lo que exige un administrador autenticado (10.29 con
`SupportLevel1`, 10.30 a 10.33, el *Re-check* de 10.22 y la vista *To review* de 10.16) queda
pendiente: no había credencial de administrador.

### 10.1 El callback

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 10.1 | Sin firma | callback sin `X-Signature` | `401`, sin cambios en `EntraxPayments` ni en el alta | ✔ (2026-10-16, por API con el fake): `401` y sin cambios |
| 10.2 | Sin secreto en el servidor | arrancar sin `Entrax__Secret` y mandar un callback firmado con cualquier secreto | `401` (falla cerrado) y un `Error` en el log | ✔ (2026-10-16, por API con el fake): arrancado sin `Entrax__Secret`, `401` con la firma buena y con otra, y un `Error` "no hay Entrax:Secret" en el log |
| 10.3 | Firma mala | firma hecha con otro secreto, o un byte del cuerpo cambiado tras firmar | `401` | ✔ (2026-10-16, por API con el fake): `401` con otro secreto y con un byte cambiado |
| 10.4 | Firma en hexadecimal | la misma firma en hexadecimal de 64 caracteres (`[Convert]::ToHexString(...)`) | se acepta igual que en Base64 | ✔ (2026-10-16, por API con el fake): en mayúsculas y en minúsculas la firma pasa (`503` del pago aún sin pagar, no `401`) |
| 10.5 | Cuerpo grande | callback de más de 64 KB | `413`, sin procesarlo | ✔ (2026-10-16, por API con el fake, tras f467d51): `413` con `Content-Length` y con `Transfer-Encoding: chunked`, sin fila en `ErrorLogs`. Antes del arreglo devolvía `500` |
| 10.6 | Antigüedad | callback bien firmado con `timestamp` de hace 2 h, o de dentro de 2 h | `400 STALE_CALLBACK`, sin cambios | ✔ (2026-10-16, por API con el fake): `400 STALE_CALLBACK` con −2 h y con +2 h |
| 10.7 | El cuerpo no decide | callback bien firmado con `status: SUCCESS` de un QR que en el fake sigue sin pagar | `503` y el alta sigue pendiente: manda `GET /payment`, no el aviso; Entrax reintentará | ✔ (2026-10-16, por API con el fake): `503` y el alta sigue pendiente |
| 10.8 | Replay | tras `POST api/v1/dev/entrax/{paymentId}/paid`, el mismo callback bien firmado dos veces | las dos `200`; un solo `PaymentHistory` `Entrax`, una sola activación | ✔ (2026-10-16, por API con el fake): dos `200` y un solo `PaymentHistory` |
| 10.9 | `orderId` que no casa | callback con el `paymentId` de un alta y el `orderId` de otra | `400 INVALID_CALLBACK`; ninguna de las dos altas cambia | ✔ (2026-10-16, por API con el fake): `400 INVALID_CALLBACK` en los dos sentidos; ningún pago cambia |
| 10.10 | Pago que no es nuestro | callback firmado con un `paymentId` inventado | `404`, sin cambios | ✔ (2026-10-16, por API con el fake): `404` |

**10.5 corregida (f467d51).** Antes, Kestrel lanzaba `BadHttpRequestException` (413) al leer el
cuerpo fuera del `try` del controlador y salía `500 INTERNAL_ERROR` con una fila en `ErrorLogs`.
Ahora el endpoint mira `Content-Length` y captura esa excepción: responde `413` y solo deja un Warning.

### 10.2 El endpoint de estado

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 10.11 | Las dos referencias | `GET .../entrax-status` sin `ref`, con el `ref` de otra alta o con un `signupId` inventado | `404 ENTRAX_PAYMENT_NOT_FOUND`, sin más datos | ✔ (2026-10-16, por API con el fake): `404 ENTRAX_PAYMENT_NOT_FOUND` sin `ref`, con el `ref` de otra alta y con un `signupId` inventado |
| 10.12 | Qué devuelve | `GET .../entrax-status` correcto | solo `status`, `expiresAt` y `secondsRemaining`: ni tokens, ni enlace del QR, ni importes, ni datos del aspirante | ✔ (2026-10-16, por API con el fake): solo `status`, `expiresAt` y `secondsRemaining` |
| 10.13 | Freno de 15 s en servidor | varias consultas seguidas (o cuatro a la vez) del mismo pago | en `EntraxApiCalls` hay como mucho una `GetPayment` de la pantalla (`Entrax:Poll`) cada 15 s: el turno se reserva con un `UPDATE` condicional sobre `LastCheckedAt` | ✔ (2026-10-16, por API con el fake): 4 consultas a la vez y 6 seguidas → una sola `GetPayment` `Entrax:Poll` |
| 10.14 | Rate limit | más de 30 consultas en un minuto desde la misma IP | `429` a partir de la 31.ª | ✔ (2026-10-16, por API con el fake): `429` desde la 31.ª |
| 10.15 | Alta ya completada | consultar un QR `AwaitingPayment` de un alta que ya se completó por otro medio | `Superseded` con `secondsRemaining: 0`; la pantalla dice "alta completada, inicia sesión" y oculta el QR | ☐ pendiente: con un QR vivo ningún método completa el alta (10.25), así que hay que confirmar una cripto desde el admin, y no hay credencial de administrador |

### 10.3 Liquidación: importe, unicidad y reactivaciones

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 10.16 | Monto menor | `POST api/v1/dev/entrax/{paymentId}/underpaid` y `POST api/v1/dev/entrax/sweep` | `ToReview` `AMOUNT_TOO_LOW`, alta sin activar, el pedido vuelve a `Pending` (H22) para pagar con otro método, `Critical` en el log, visible en *To review* | ✔ (2026-10-16, por API con el fake): `ToReview` `AMOUNT_TOO_LOW` (pagado 1 USD de menos), sin `PaymentHistory` y con `Critical`; entonces el pedido se quedaba en `Processing`, corregido después (H22: vuelve a `Pending`; pendiente de repetir con el fake). La vista *To review*, pendiente (admin) |
| 10.17 | Doble cobro | activar un alta con un QR y luego pagar en el fake un segundo QR del mismo pedido (el que quedó `Superseded`) y barrer | el segundo queda `ToReview` `ORDER_ALREADY_SETTLED`, sin segunda activación | ✔ (2026-10-16, por API con el fake), con el QR caducado en vez de uno `Superseded` (por API no se llega a dos QR vivos): `ToReview` `ORDER_ALREADY_SETTLED`, `Critical` y un solo `PaymentHistory` |
| 10.18 | Carrera | con un QR pagado en el fake, cuatro `entrax-status` a la vez más un `sweep` | un solo `PaymentHistory` `Entrax`, una sola fila de cada bono por `SourceOrderId`, el pago `Paid` una vez y ninguno `ToReview`: el pago se reclama antes que el pedido, con un `ExecuteUpdate` condicional | ✔ (2026-10-16, por API con el fake), dos veces (4 estados + barrido; 4 estados + 2 barridos + 4 callbacks): un `PaymentHistory`, una fila por bono (tipos 2 y 34) y ningún `ToReview` |
| 10.19 | QR caducado | `POST api/v1/dev/entrax/{paymentId}/expired` | el pago `Expired`; el pedido vuelve a `Pending` solo si estaba en `Processing` y nada más lo espera (vuelta condicional) | ✔ (2026-10-16, por API con el fake): `Expired` y el pedido de `Processing` a `Pending` |
| 10.20 | Caducidad local (H20) | dejar el QR en `pending` (o sin credenciales) más allá de `ExpiresAt` + `Entrax:LocalExpiryGraceMinutes` y barrer | `Expired` con `FailureCode = LOCAL_EXPIRY` y el pedido de vuelta a `Pending`; si luego resulta pagado, el barrido de pagos tardíos lo vuelve a mirar | ✔ (2026-10-16, por API con el fake, con `ExpiresAt` atrasado a mano en la BD local): dentro de la gracia sigue esperando; pasada, `Expired` `LOCAL_EXPIRY` y el pedido a `Pending`; pagado después, el barrido lo activa (el `FailureCode` `LOCAL_EXPIRY` se queda en la fila ya `Paid`) |
| 10.21 | Sustituidos (H21) | activar un alta con un QR teniendo otro `AwaitingPayment` del mismo pedido | el otro pasa a `Superseded`; el barrido lo sigue mirando mientras pueda pagarse | ☐ pendiente: por API no se llega a dos QR `AwaitingPayment` del mismo pedido (10.24 y 10.25 lo impiden); solo lo cubren las pruebas unitarias |
| 10.22 | Sin reactivar a quien desactivó un administrador | alta pagada y activa; un administrador la desactiva (miembro `Inactive` o `Terminated`); repetir el callback y hacer un *Re-check* | `AlreadyResolved` y la cuenta **sigue inactiva** | ✔ en parte (2026-10-16, por API con el fake): con el miembro puesto `Inactive` en la BD local, el callback (`200`) y el barrido no lo reactivan. El *Re-check*, pendiente (admin) |

### 10.4 El alta con otros métodos mientras hay un QR

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 10.23 | País no configurado | `complete` con `"paymentMethod": 5` desde un país fuera de `Entrax:Countries` (o con Entrax apagado) | `400 ENTRAX_NOT_AVAILABLE` | ✔ (2026-10-16, por API con el fake): un alta de `ES` recibe `400 ENTRAX_NOT_AVAILABLE`; con Entrax apagado, lo mismo y `payment-options` vacío |
| 10.24 | Dos QR a la vez | dos `complete` con Entrax simultáneos del mismo alta | uno recibe el QR y el otro `409 ENTRAX_IN_PROGRESS` (o `ENTRAX_QR_ALREADY_ACTIVE` si llega cuando el primero ya terminó) | ✔ (2026-10-16, por API con el fake): uno `200` con el QR y otro `409 ENTRAX_IN_PROGRESS`; un solo pago |
| 10.25 | QR vivo en otra ventana | con un QR vivo, `complete` con **cualquier** método (Entrax, tarjeta, token, cripto) | `409 ENTRAX_QR_ALREADY_ACTIVE`, sin tocar nada | ✔ (2026-10-16, por API con el fake): Entrax, tarjeta, token y cripto → `409 ENTRAX_QR_ALREADY_ACTIVE` |
| 10.26 | QR caducado sin cerrar | con un QR ya caducado que el servidor aún no cerró, `complete` con cualquier método | `409 ENTRAX_PAYMENT_CONFIRMING` | ✔ (2026-10-16, por API con el fake, con `ExpiresAt` atrasado a mano en la BD local): token, Entrax y cripto → `409 ENTRAX_PAYMENT_CONFIRMING` |
| 10.27 | Alta ya pagada | alta completada con Entrax; `complete` con tarjeta o token | `409 SIGNUP_ALREADY_SETTLED`; la tarjeta y el token reclaman el pedido solo desde `Pending` con un `UPDATE` condicional y no activan nada | ✔ (2026-10-16, por API con el fake): tarjeta, token, cripto y Entrax → `409 SIGNUP_ALREADY_SETTLED`, y también `select-products`; nada cambia |
| 10.28 | El 409 no delata pedidos | `complete` con el id de un pedido completado que **no** es el de un alta (una renovación o una compra: el del alta es el primer pedido del miembro) | `400 SIGNUP_NOT_FOUND`, nunca `SIGNUP_ALREADY_SETTLED` | ✔ en parte (2026-10-16, por API): en la BD local no hay ningún pedido completado que no sea de alta; con renovaciones `Paid` y `Processing` → `400 SIGNUP_NOT_FOUND`, y con el primer pedido completado de un miembro → `409` |

### 10.5 Admin

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 10.29 | Roles | `GET .../admin/entrax-signups` y los dos `recheck` (AdminAPI y SignupAPI) con un token de `SupportLevel1` | `403`; en AdminWeb la entrada *Entrax Signups* no aparece | ✔ en parte (2026-10-16, por API con el fake): sin token `401`, y con el token de un `Ambassador` `403` en la lista (las tres vistas) y en los dos `recheck`. Con `SupportLevel1` y el menú de AdminWeb, pendiente (no hay credencial) |
| 10.30 | La lista no expone | respuesta de `GET .../admin/entrax-signups?view=open` en las herramientas del navegador | sin `QrLink` (con él se puede pagar), sin `Notes`, sin cuerpos ni firmas de Entrax | ☐ pendiente: necesita un administrador autenticado y no hay credencial |
| 10.31 | Recientes | `view=recent` | solo pagos de los últimos 30 días; *open* y *review* sin límite de fecha | ☐ pendiente: necesita un administrador autenticado y no hay credencial |
| 10.32 | Re-check auditado | *Re-check* sobre un pago; repetirlo cerrando la pestaña antes de la respuesta | una fila en `AuditTracking` (`Action = EntraxRecheck`) con quién, el estado anterior, el código HTTP y el desenlace; la cancelada con `CLIENT_CANCELLED`. El Bearer no se guarda | ☐ pendiente: necesita un administrador autenticado y no hay credencial |
| 10.33 | Re-check con SignupAPI caída o lenta | parar SignupAPI (o hacer que tarde más de 20 s) y pulsar *Re-check* | `502 SIGNUP_API_UNAVAILABLE` en ≤ 20 s, auditado; repetirlo es seguro (idempotente) | ☐ pendiente: necesita un administrador autenticado y no hay credencial |
| 10.34 | Arranque sin `Services:SignupAPI` | arrancar AdminAPI con `ASPNETCORE_ENVIRONMENT=Production` sin `Services__SignupAPI` | no arranca (`InvalidOperationException`); con ella, valida el certificado de SignupAPI | ☐ pendiente: en `Production` AdminAPI se para antes en `Jwt:PrivateKeyBase64` (vive en los secretos de usuario, que `Production` no carga) y sin pasarle la llave de firma no se llega a la comprobación de `Services:SignupAPI` |

### 10.6 Configuración y endpoints de desarrollo

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 10.35 | Fake fuera de Development | arrancar SignupAPI en `Production` con `Entrax__UseFake=true` | no arranca | ✔ (2026-10-16, `Production` en otro puerto contra la BD local): no arranca, `InvalidOperationException` "[Entrax] … UseFake solo se permite en Development" |
| 10.36 | Endpoints de desarrollo | `POST api/v1/dev/entrax/{id}/paid` en Development sin `UseFake` | `404`: solo existen con `IsDevelopment` **y** `UseFake` (y fuera de Development `UseFake` no arranca) | ✔ (2026-10-16, por API, Development sin `UseFake`): `404` en `paid` y en `sweep` |
| 10.37 | Credenciales a medias | arrancar en `Production` con `Entrax__Enabled=false` y solo `Entrax__KeyId` | no arranca | ✔ (2026-10-16, `Production` en otro puerto contra la BD local): no arranca, "credenciales a medias" |
| 10.38 | Encendido sin callback https | `Production`, `Enabled=true`, credenciales y `Entrax__CallbackUrl=http://…` | no arranca | ✔ (2026-10-16, `Production` en otro puerto contra la BD local): no arranca, "CallbackUrl debe ser una URL https absoluta" |
| 10.39 | Apagado con pagos esperando | `Enabled=false` con un pago `AwaitingPayment` y ejecutar el barrido | `Critical` en el log y el pago se sigue liquidando | ✔ en parte (2026-10-16, por API con el fake): `Critical` "Entrax está apagado y hay 1 pagos…" y el barrido sigue llamando a `GetPayment`; que liquide no se ve con el fake, que pierde sus pagos al reiniciar |
| 10.40 | El barrido vivo | `/hangfire` de SignupAPI | `entrax-signup-sweep` existe, cada 2 minutos, cola `signups` | ✔ (2026-10-16, tablas de Hangfire): `*/2 * * * *`, cola `signups` y un servidor que atiende `signups` |

### 10.7 Fallos del legado (MWR Life) que no se copiaron

Cada uno era un fallo real de la integración de Entrax en MWR Life. Se prueban para que no vuelvan.

| # | Fallo del legado | Cómo se prueba | Resultado esperado | ✔ |
|---|---|---|---|---|
| 10.41 | El monto lo mandaba el cliente | `complete` con Entrax añadiendo `"amount": 1` (o cualquier campo de importe) al cuerpo | `CompleteSignupRequest` no tiene importe: el QR sale por `Orders.TotalAmount` en USD (`EntraxPayments.AmountUsd` = total del pedido) | ✔ (2026-10-16, por API con el fake): con `"amount": 1` en el cuerpo el QR sale por 458,94 USD, el `Orders.TotalAmount` |
| 10.42 | Un mismo pago valía para varias altas | 10.9, y en SQL intentar dar el mismo `ExternalPaymentId` a dos filas, o `Consumed = 1` a dos pagos del mismo pedido | `400 INVALID_CALLBACK`; los índices únicos `IX_EntraxPayments_ExternalPaymentId` y `UX_EntraxPayments_OrderId_Consumed` lo impiden | ✔ (2026-10-16, por API con el fake): 10.9, y en SQL (con `ROLLBACK`) los índices únicos rechazan un `ExternalPaymentId` y un `ExternalOrderId` repetidos y un segundo `Consumed = 1` del mismo pedido |
| 10.43 | La contraseña del formulario en claro en el log | hacer un alta con una contraseña reconocible y buscarla en el log de SignupAPI y en `EntraxApiCalls` | no aparece; `EntraxApiCalls` solo tiene campos estructurados, sin cuerpos | ✔ (2026-10-16, por API con el fake): ninguna de las 8 contraseñas aparece en los logs, en `EntraxApiCalls` ni en `ErrorLogs` |
| 10.44 | El webhook no activaba el alta (lo hacía el navegador) | cerrar la pestaña tras emitir el QR, `POST api/v1/dev/entrax/{paymentId}/paid` y mandar el callback firmado (o esperar al barrido) | el alta queda activa sin nadie en la página | ✔ (2026-10-16, por API con el fake): altas activadas por el barrido sin nadie consultando el estado (Tarea 22, paso 5, y 10.20) |
| 10.45 | La firma se comparaba con `==` | revisar `EntraxSignature.VerifyCallback` | HMAC-SHA256 sobre el cuerpo crudo, comparado con `CryptographicOperations.FixedTimeEquals` | ✔ (2026-10-16, revisión del código): `HMACSHA256.HashData` sobre el cuerpo crudo y `CryptographicOperations.FixedTimeEquals` |
| 10.46 | Endpoints anónimos que devolvían `ex.ToString()` | provocar errores en los anónimos: callback firmado con un cuerpo que no es JSON, `entrax-status` con la base parada | `400 INVALID_CALLBACK`, o `500 INTERNAL_ERROR` ("An unexpected error occurred." y el `traceId`) sin traza ni texto de la excepción; el detalle solo en el log | ✔ en parte (2026-10-16, por API con el fake): un cuerpo que no es JSON → `400 INVALID_CALLBACK`; los `500` salen con "An unexpected error occurred." y sin traza, pero con el `traceId` vacío. Con la base parada no se probó (es la SQL Server local compartida) |
| 10.47 | Conversión de divisa con el tipo de cambio de Spreedly (y USD mandados como rublos si fallaba) | mirar la petición del QR y `EntraxPayments` | el QR se pide **en USD** y nunca convertimos la divisa: `AmountRub` y `ExchangeRate` los da Entrax. Un QR que vuelve en otra moneda o por otro importe no se muestra (`502 ENTRAX_UNAVAILABLE` y `Critical`), y un pago en otra moneda va a `ToReview` `CURRENCY_MISMATCH` | ✔ en parte (2026-10-16, por API con el fake): el QR se pide en USD y `AmountRub` y `ExchangeRate` los da el proveedor (95,50 del fake). El `502` por otra moneda y `CURRENCY_MISMATCH` no se pueden provocar con el fake |

## 12. Reportes del BizCenter (Plan J)

Qué protege: seis reportes nuevos o arreglados en el BizCenter: *Mis enlaces*, *Residuales diarios*,
*Clientes del equipo* (Personales/Equipo), *Actividad por fechas*, *Cambios de estado* (inscripción,
Dual Team y clientes) y *Eventos del equipo* (solo el reporte: la importación de inscripciones queda
para otro plan). Tres reglas que estas pruebas comprueban: **el miembro sale siempre del token**
(ningún endpoint nuevo acepta `memberId`), **el alcance lo pone el servidor** (el nodo de genealogía
**más reciente** del que mira y de cada miembro, con un prefijo que acaba siempre en «/»; en el
binario, fecha primero y pertenencia después) y **la hora es la del servidor**. Plan:
`docs/superpowers/plans/2026-10-16-plan-j-reportes-bizcenter.md`.

Endpoints (BizCenter, `https://localhost:7003` en Development, salvo el de AdminAPI):

| Endpoint | Qué hace |
|---|---|
| `GET api/v1/bizcenter/my-links` | enlaces personales (con el slug del token) y corporativos |
| `GET api/v1/bizcenter/commissions/dual-residual/daily?year=&month=&page=&pageSize=` | historial de `DailyResidualEarnings` con totales |
| `POST api/v1/bizcenter/team/enrollment/customers/grid?scope=personal\|team` | clientes; contacto solo de los personales |
| `GET api/v1/bizcenter/team/reports/setup` | rango por defecto, tope común (366 días) y primer día de *Actividad* (hoy − 3 meses) |
| `GET api/v1/bizcenter/team/enrollment/activity/columns` y `POST …/activity/grid?from=&to=` | actividad por fechas |
| `POST api/v1/bizcenter/team/changes/{enrollment\|dual\|customers}/grid?from=&to=` | cambios de estado |
| `GET api/v1/bizcenter/events`, `POST events/{id}/team-registrations/grid?registeredOnly=`, `GET events/{id}/team-registrations/totals` | eventos del equipo |
| `GET api/v1/admin/members/{memberId}/commissions/dual-residual/daily` y `POST …/team/enrollment/customers/grid?scope=` — AdminAPI | lo mismo para el perfil de miembro del Admin |

**Condiciones de despliegue:**

- **Orden de las migraciones** (todas las aplica `MigrateAsync` al arrancar cualquier servicio):
  `IndiceResidualesPorBeneficiarioYFecha`, `IndiceResidualesConImporteYEstado`,
  `IndicePatrocinadorYFechaDeAlta`, `IndiceFechaDeAlta`, `HistorialDeEstados`,
  `MarcaMantenerCalificado`, `InscripcionesAEventos`. Alteran o indexan tablas de ~120.000 filas:
  desplegar fuera de horas de tráfico. `IndicePatrocinadorYFechaDeAlta` pasa
  `MemberProfiles.SponsorMemberId` a `nvarchar(450)` y `HistorialDeEstados` hace lo mismo con
  `MemberStatusHistories.MemberId`; las dos se niegan a truncar (`THROW 51001` y `51003` si algún
  valor pasa de 450 caracteres).
- **`GenealogyTree.HierarchyPath` no se toca** en este plan (sin índice: mejora de rendimiento futura).
- **R16, riesgo de producción.** *Actividad*, *Clientes* y *Cambios de estado* leen `Orders` y
  `OrderDetails` enteros (sin índice en `Orders.MemberId` ni en `OrderDetails.OrderId`). En
  `READ COMMITTED` sin snapshot pueden chocar con la facturación, y la víctima de un *deadlock*
  puede ser el escritor (una renovación o un cobro). Antes de desplegar, comprobar en producción
  `SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()` (en la base local
  es `0`) y decidir con Nick: índices de pedidos, `READ_COMMITTED_SNAPSHOT` o ambos. Resultado en
  producción: ________.
- **«Calificado» sin anualidad.** «Calificado» = membresía activa Y (alta hace menos de 12 meses, O
  anualidad pagada, O `MemberProfile.KeepQualified`). La anualidad todavía no está modelada (Plan K):
  **mientras tanto, todo miembro que se inscribió hace más de 12 meses y no tiene `KeepQualified`
  sale como «No calificado»** en *Mi equipo*, el detalle de una rama, *Mi equipo* del Dual Team,
  *Cambios de estado* y *Eventos*. Solo afecta a lo que se ve en pantalla (no a comisiones ni a
  rangos). Avisar a soporte antes de desplegar; la marca `KeepQualified` no se edita aún desde el Admin.
- **Dominios de *Mis enlaces*** confirmados por Nick (pregunta P1) en `MyLinks:Links` del
  `appsettings.json` del BizCenter. Una plantilla inválida impide arrancar el BizCenter. Un
  `appsettings.{Entorno}.json` fusiona la lista **por índice**: para cambiarla hay que repetirla entera.
- **El historial de estados empieza al desplegar el Bloque 5.** Antes, *Cambios de estado* sale vacío
  y las fechas de «último cambio» de la membresía son las de reserva (la propia del estado). Fecha del
  despliegue: ________.
- **Primer ciclo de facturación recurrente tras el Bloque 5**: comparar su duración con la del
  anterior (el interceptor recorre el `ChangeTracker` en cada guardado y añade una fila por cambio de
  estado, R14 y R17).
- **Eventos sin importación**: el reporte sale vacío hasta que exista el importador (plan aparte). Con
  «Todo el equipo» recorre los perfiles del equipo (R16); «Solo inscritos» y los totales parten de
  las inscripciones del evento. Medición de la raíz local: `SqlServerEventosTests`.
- **Conciliación de facturación (fallo anterior al plan).** `RecurringBillingProcessor` dejaba
  `Stopped` el estado de facturación pero la membresía seguía `Active` cuando se pasaba el tope de
  días sin facturar (arreglado en este plan). Antes de desplegar, ejecutar en producción esta consulta
  de **solo lectura** y pasar el resultado a Nick, que decide qué se hace con esas membresías; **no se
  corrigen en silencio** ni con un script automático:

  ```sql
  -- SubscriptionBillingStates.Status 4 = Stopped (RecurringBillingStatus)
  -- MembershipSubscriptions.SubscriptionStatus 1 = Active (MembershipStatus)
  SELECT s.Id AS BillingStateId, s.MemberId, s.MembershipSubscriptionId, s.RecurringBillingPlanId,
         s.LastSuccessfulBillingDate, s.LastUpdateDate, m.SubscriptionStatus, m.MembershipLevelId
  FROM SubscriptionBillingStates s
  JOIN MembershipSubscriptions m ON m.Id = s.MembershipSubscriptionId
  WHERE s.Status = 4
    AND m.SubscriptionStatus = 1
    AND m.IsDeleted = 0
  ORDER BY s.LastUpdateDate;
  ```

  En la base local devuelve 33 filas. Resultado en producción: ________.
- **Antes del plan de importación de inscripciones**: decidir la longitud de
  `MemberProfiles.MemberId` (hoy 20; el cliente tiene ids de hasta 50). Ampliarla obliga a soltar y
  recrear las claves foráneas de `MembershipSubscriptions` y `CorporateEventRegistrations` y el índice
  de la clave alterna.
- **Fuera de este plan, pendiente**: índices de `Orders.OrderNo`, `Orders.MemberId` y
  `OrderDetails.OrderId` (R9 y R16); retirar `commissions/dual-residual` (J29).

### Pruebas

Preparación: SignupAPI (`https://localhost:7005`), BizCenter (`https://localhost:7003`),
BizCenterWeb (`https://localhost:7004`), AdminAPI (`https://localhost:7002`) y AdminWeb
(`https://localhost:7001`) arrancados con el perfil `https` contra la base local, con las siete
migraciones aplicadas. Dos embajadores sin relación entre sí (A y B), un descendiente de A con
clientes en dos niveles, y un administrador. `TOKEN_A` es el Bearer de A.

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 12.1 | IDOR en *Mis enlaces* | `GET /api/v1/bizcenter/my-links?memberId={B}` con `TOKEN_A` | los enlaces de A (con el slug de A); el parámetro se ignora | ☐ |
| 12.2 | IDOR en residuales | `GET /api/v1/bizcenter/commissions/dual-residual/daily?memberId={B}&year=2026` con `TOKEN_A` | el historial de A | ☐ |
| 12.3 | IDOR en las rejillas | añadir `?memberId={B}` y `"memberId":"{B}"` al cuerpo de `POST team/enrollment/activity/grid`, `team/changes/enrollment/grid`, `team/changes/dual/grid`, `team/changes/customers/grid`, `team/enrollment/customers/grid` y `events/{id}/team-registrations/grid` con `TOKEN_A` | se ignoran: siempre el equipo de A (lo vigila además `MiembroDelTokenTests`) | ☐ |
| 12.4 | Alcance del equipo de inscripción | con `TOKEN_A`, actividad y cambios de inscripción en un rango donde B y la upline de A tienen altas y cambios | nunca aparecen B, su equipo ni la upline de A; la fila de A en actividad tiene nivel 0 | ☐ |
| 12.5 | Nodo obsoleto (R8) | un miembro con un nodo antiguo bajo A y el actual bajo B | no sale en los reportes de A ni lo deja ver `DownlineGuard` | ☐ |
| 12.6 | Prefijo con barra | dos embajadores `X-1` y `X-10`; con el token de `X-1` | nada del equipo de `X-10` en ningún reporte ni guarda | ☐ |
| 12.7 | Alcance del Dual Team | un miembro del binario de A que no es de su inscripción y otro fuera de los dos, ambos con cambios en el rango | el primero sale en `changes/dual` (con su pierna) y no en `changes/enrollment`; el segundo en ninguna | ☐ |
| 12.8 | Contacto solo de personales | `POST team/enrollment/customers/grid` (sin `scope`) con `TOKEN_A` | `email` y `phone` solo en los clientes de nivel 1; `null` en los demás. Con `?scope=personal`, solo los de nivel 1 | ☐ |
| 12.9 | La búsqueda no delata | el mismo endpoint con `"search"` = parte del correo de un cliente de nivel 2, y con un filtro de columna `Email contains` ese texto | 0 resultados en los dos | ☐ |
| 12.10 | El `GET` antiguo también | `GET /api/v1/bizcenter/team/enrollment/customers` con `TOKEN_A` | mismo anulado que 12.8 | ☐ |
| 12.11 | Rango de *Actividad* | `from` de hace 4 meses; `to` de mañana; `to` anterior a `from` | `400 REPORT_RANGE_TOO_OLD`; `400 REPORT_RANGE_IN_FUTURE`; `400 REPORT_RANGE_INVALID`; la pantalla muestra el texto de cada uno | ☐ |
| 12.12 | Rango de *Cambios de estado* | `from=2024-01-01&to=2025-01-01`; un rango de 6 meses atrás | `400 REPORT_RANGE_TOO_LONG`; el de 6 meses vale (el límite de 3 meses es solo de *Actividad*) | ☐ |
| 12.13 | Vista desconocida | `POST team/changes/binary/grid` | `404 REPORT_SCOPE_NOT_FOUND` | ☐ |
| 12.14 | La ruta binaria no sale | respuesta de `team/changes/dual/grid` en las herramientas del navegador | sin `dualPath` ni ninguna ruta del árbol | ☐ |
| 12.15 | Evento que no se ve | `events/{id de un evento desactivado}/team-registrations/grid` y `/totals` | `404 EVENT_NOT_FOUND`; `GET events` no lo lista | ☐ |
| 12.16 | Eventos sin importación | un evento activo sin inscripciones | rejilla vacía, totales 0 y el aviso de que aparecerán cuando se importen | ☐ |
| 12.17 | Totales del equipo | inscripciones (sembradas a mano en SQL) de miembros fuera del equipo de A en el mismo evento | no cuentan ni en filas ni en totales | ☐ |
| 12.18 | Suplantación de solo lectura | un administrador suplanta a A con token de solo lectura y abre las pantallas del plan | las rejillas `POST` responden `200` (`ReadOnlySafe`) con datos de A; ningún endpoint del plan escribe | ☐ |
| 12.19 | Suplantación y enlaces | durante la suplantación, *Mis enlaces* | el slug del suplantado; copiar no escribe nada | ☐ |
| 12.20 | Plantilla de enlace inválida | arrancar BizCenter con `MyLinks__Links__0__Url=http://mwrlife.com/{slug}` | no arranca: `InvalidOperationException` «[MyLinks] … https» | ☐ |
| 12.21 | Slug con caracteres raros | un slug con espacio o acento | sale escapado en la URL (`%20`, `%C3%B3`) | ☐ |
| 12.22 | Residuales sin débitos | comparar el total del mes con `SELECT SUM(Amount) FROM DailyResidualEarnings WHERE BeneficiaryMemberId = @A AND Status <> 3 AND EarnedDate >= @desde AND EarnedDate < @hasta` | coinciden; no aparecen débitos negativos ni consolidaciones de `CommissionEarnings`; las filas `Paid` se rotulan «Consolidado» | ☐ |
| 12.23 | Hora de servidor | cambiar la zona horaria del navegador a UTC+14 y volver a abrir residuales, actividad y cambios | el año por defecto y los rangos por defecto no cambian (son del servidor) | ☐ |
| 12.24 | Historial | cambiar desde el Admin el estado de A con un motivo; activar un alta; forzar que una renovación pase a `HoldByBilling` | una sola fila en `MemberStatusHistories` con el motivo; una fila `Pending → Active`; una fila en `MembershipStatusHistories` con autor `system` si el guardado no puso `LastUpdateBy`. Caso límite: si el guardado vuelve a poner el **mismo** `LastUpdateBy` que ya tenía la fila, no cuenta como cambio y el autor queda `system`; es a propósito, falla hacia el lado seguro (nunca atribuye el cambio a quien no lo hizo) | ☐ |
| 12.25 | Corte de facturación | un estado de facturación que pase el tope de días sin facturar | el estado queda `Stopped` y la membresía en `HoldByBilling` (o `Expired` según el plan), guardado en el mismo paso | ☐ |
| 12.26 | «Calificado» | un miembro activo inscrito hace más de 12 meses, sin `KeepQualified`; después con `KeepQualified = 1` (en SQL) | «No calificado» y luego «Calificado» en *Mi equipo* y *Cambios de estado* | ☐ |
| 12.27 | Nivel por la orden | un miembro cuya orden de alta lleva Lifestyle Ambassador y Elite y cuya suscripción dice otro nivel | *Actividad* lo cuenta en la columna Elite; *Clientes* y *Cambios de estado* muestran Elite | ☐ |
| 12.28 | Menú | abrir `/team/residuals`, `/team/dual-changes`, `/team/enrollment/activity`, `/team/enrollment/changes`, `/team/enrollment/customer-changes`, `/events` y `/my-links` | cada grupo correcto queda abierto y la entrada marcada; las mismas rutas existen en la app MAUI | ☐ |
| 12.29 | Primer ciclo de facturación tras el Bloque 5 | comparar la duración del ciclo con el anterior | sin degradación apreciable | ☐ |

## 13. Cuota anual del Biz Center (Plan K)

Qué protege: la cuota anual de $99 de cada Ambassador, cobrada **aparte** de la membresía con un
estado de cobro anual por miembro (`SubscriptionBillingStates` con `Purpose = 2` y sin suscripción),
por el único motor de renovación que queda (`RecurringBillingProcessor`; `MembershipAutoRenewalJob`
apagado), y el botón «Pagar ahora» del perfil. Cuatro reglas que estas pruebas comprueban: **quien paga
sale del token** (ningún endpoint acepta `memberId`), **pagar con lo que ya es del miembro exige step-up
y nunca se hace suplantando**, **un ciclo no se cobra dos veces** (candado de fondos + índice único de
pagos vivos por ciclo) y **nunca se cobra con tarjeta** (la tarjeta queda para el Plan I y su gateway es
un stub). Plan: `docs/superpowers/plans/2026-10-17-plan-k-cuota-anual.md`.

Endpoints:

| Endpoint | Qué hace |
|---|---|
| `GET api/v1/bizcenter/billing/annual-fee` (BizCenter, 7003) | resumen de la cuota del miembro del token |
| `PUT api/v1/bizcenter/billing/annual-fee/preference` | pagar las cuotas con comisiones y eWallet (sí/no) |
| `POST api/v1/bizcenter/billing/annual-fee/pay` | «Pagar ahora» (`Token`, `CommissionBalance`, `EWallet`); exige `X-Step-Up-Token` de `member.annual-fee.pay-from-balance` |
| `POST api/v1/billing/annual-fee/pay` (Billing) | lo mismo, llamado por el BizCenter con el JWT del miembro; vuelve a validar y quema el permiso |
| `POST api/v1/billing/annual-fee/payments/{id}/reconcile` (Billing) | tesorería (`SuperAdmin`, `Admin`, `BillingManager`) cierra un pago a conciliar |
| `GET api/v1/admin/members/{memberId}/billing/annual-fee` (AdminAPI, 7002) | resumen en el perfil de miembro, solo lectura |

Trabajos de Hangfire (Billing): `annual-fee-state-enrollment` (alta de estados; **registrado apagado**
con `Cron.Never()` salvo `Billing:AnnualFee:EnrollmentJobEnabled = true`, que lo programa a las 4:30 UTC; se
lanza a mano desde `/hangfire`), `annual-fee-stale-pending` (cada hora, a los 45; solo pasa `Pending` a
conciliar). `membership-auto-renewal` ya no existe.

**Condiciones de despliegue:**

- **Encender el alta de estados y el cobro automático a la vez** (revisión final, I-A). En cuanto existe el
  estado de cuota de un Ambassador, «anualidad pagada» (rama (b) de «Calificado») depende de él: el día de su
  aniversario el estado vence y, si el cobro automático (`Billing:AnnualFee:AutoChargeEnabled`) sigue apagado
  y el miembro no paga a mano con «Pagar ahora», **pierde esa rama** y puede pasar a «No calificado» (si no
  tiene `KeepQualified` ni se inscribió hace menos de 12 meses). Cuanto más tiempo pase entre lanzar
  `annual-fee-state-enrollment` y encender el cobro, más Ambassadors caen así. **Recomendación: encender los
  dos a la vez** (lanzar el alta y poner el interruptor en `true` el mismo día). La decisión es de Nick.
  Fechas: alta ________, cobro ________.
- **Migración `CuotaAnual`** (la aplica `MigrateAsync` al arrancar cualquier servicio): altera
  `SubscriptionBillingStates` (columna `MembershipSubscriptionId` a anulable, `Purpose`, índices y
  `CHECK`), añade `FeesFromBalance` a `MemberProfiles` y `IsAnnualFeePlan` a `RecurringBillingPlans`, y
  crea `AnnualFeePayments`. Se niega a migrar con `THROW 51010` (más de un plan «Lifestyle Ambassador
  (Annual)») o `THROW 51011` (estados por suscripción en ese plan). Antes, en producción:

  ```sql
  SELECT COUNT(*) AS planesAnuales FROM RecurringBillingPlans WHERE Name = N'Lifestyle Ambassador (Annual)';
  SELECT COUNT(*) AS estadosEnPlanAnual FROM SubscriptionBillingStates s
  JOIN RecurringBillingPlans p ON p.Id = s.RecurringBillingPlanId WHERE p.Name = N'Lifestyle Ambassador (Annual)';
  ```

  Esperado: `1` y `0`. Resultado en producción: ________. Desplegar fuera de horas de tráfico.
- **Conciliación de §12 antes del Bloque 1.** Apagar `MembershipAutoRenewalJob` deja de renovar por
  esa vía los estados `Stopped` con la membresía `Active` (33 en local). Ejecutar antes la consulta de
  §12, con `AND s.Purpose = 1` para dejar fuera los estados de cuota, y que Nick decida. Resultado: ____.
- **`membership-auto-renewal` borrado de Hangfire**: tras el primer arranque de Billing con el Bloque 1,
  en `/hangfire` → *Recurring jobs* no aparece. Comprobado: ________.
- **Alta de estados**: tras desplegar el Bloque 2, lanzar a mano `annual-fee-state-enrollment` desde
  `/hangfire` y comprobar:

  ```sql
  SELECT COUNT(*) AS ambassadors FROM MemberProfiles WHERE MemberType = 0 AND IsDeleted = 0 AND Status <> 4;
  SELECT COUNT(*) AS estadosDeCuota FROM SubscriptionBillingStates WHERE Purpose = 2;
  SELECT MIN(NextBillingDate) AS primerVencimiento FROM SubscriptionBillingStates WHERE Purpose = 2;
  ```

  Esperado: los dos recuentos iguales y el primer vencimiento **hoy o después** (no se cobra hacia
  atrás). En local: ~119.445. Resultado en producción: ________.
- **El cobro forzado también exige el interruptor** (revisión I-3): con `Billing:AnnualFee:AutoChargeEnabled` apagado, el
  bill-now de soporte sobre un estado de cuota responde `Skipped` con `ANNUAL_FEE_AUTO_CHARGE_DISABLED` y no cobra.
  «Pagar ahora» del miembro no depende del interruptor.
- **Quien cancela no paga la cuota en automático** (decisión de Nick, M-8): con la membresía vigente cancelada, el cobro
  se pospone cada día sin mover el vencimiento hasta que reactive; «Pagar ahora» sigue disponible. Comprobar con un
  Ambassador de prueba que cancela: su estado de cuota queda con `LastFailureReason = MEMBERSHIP_CANCELLED` y sin
  intentos. Comprobado: ________.
- **Conciliación de la cuota** (revisión M-3/M-7): cada conciliación deja su línea `[reconciled … by …]` en las notas
  del pago. Un `ANNUAL_FEE_RECONCILED_CYCLE_ALREADY_ADVANCED` es un posible doble cobro: revisar y devolver si procede.
- **Cobro automático apagado** (`Billing:AnnualFee:AutoChargeEnabled`, por defecto `false`). Se enciende
  solo cuando Nick lo diga (P10). Medir el primer ciclo con él encendido en un entorno de prueba (R2).
  Fecha de encendido en producción: ________.
- **La tarjeta sigue apagada** (`Billing:TokenPurchase:CardEnabled = false`, `Signup:CardEnabled = false`)
  y la cuota no la acepta en ningún caso.
- **Tarjeta de las renovaciones apagada** (`Billing:Recurring:CardEnabled`, por defecto `false`; revisión
  I1/I2). Con ella apagada, una renovación sin saldo gastable suficiente falla con `CARD_DISABLED` y sigue
  la cadencia del plan; agotado el tope de 90 días sin cobrar, el estado se para y la membresía pasa a
  `HoldByBilling`. No se enciende hasta el Plan I **y** hasta cerrar `DelayedFallbackChargeJob` (C3).
  Comprobado que no está en ningún `appsettings`: ________.
- **Rastro del stub (R10)**: los `PaymentHistory` simulados que ya existan, con su pedido y su intento
  de gateway (los escribieron el orquestador, el cobro diferido o la renovación manual cuando la tarjeta
  no estaba apagada):

  ```sql
  SELECT ph.Id, ph.OrderId, ph.MemberId, ph.Amount, ph.GatewayName, ph.GatewayTransactionId, ph.CreationDate,
         o.OrderNo, o.Status AS EstadoPedido, o.MembershipSubscriptionId
  FROM PaymentHistories ph WITH (NOLOCK)
  LEFT JOIN Orders o WITH (NOLOCK) ON o.Id = ph.OrderId
  WHERE ph.GatewayTransactionId LIKE 'simulated%' OR ph.GatewayTransactionId LIKE 'STUB-%'
  ORDER BY ph.CreationDate DESC;

  SELECT COUNT(*) AS intentosSimulados FROM GatewayChargeAttempts WITH (NOLOCK)
  WHERE GatewayTransactionId LIKE 'simulated%' OR GatewayTransactionId LIKE 'STUB-%';

  -- Renovaciones dadas por cobradas con dinero inventado antes del Bloque 1 (pedido Paid con pago simulado):
  SELECT o.Id, o.OrderNo, o.MemberId, o.TotalAmount, o.MembershipSubscriptionId, o.OrderDate
  FROM Orders o WITH (NOLOCK) JOIN PaymentHistories ph WITH (NOLOCK) ON ph.OrderId = o.Id
  WHERE o.Status = 2 AND (ph.GatewayTransactionId LIKE 'simulated%' OR ph.GatewayTransactionId LIKE 'STUB-%');
  ```

  Pasar el resultado a tesorería: no son dinero. En local (2026-10-17): 0. Resultado en producción: ________.
- **Lo que deja de renovarse al apagar `MembershipAutoRenewalJob`** (revisión I3). El job renovaba toda
  suscripción activa con `IsAutoRenew`, sin `CancellationDate`, no gratuita y con `StartDate` de hace
  30 días o más, tuviera o no estado de cobro, por `RenewMembershipHandler`. Al apagarlo dejan de
  renovarse por esa vía:
  1. **Las Ambassador de nivel 1** (Lifestyle Ambassador): el plan anual no crea estados por suscripción
     (K2), así que el motor no las renueva. Las sustituye la cuota anual del Bloque 3.
  2. **La ruta de wallet del legado** (miembros fuera de EE. UU. y Canadá): el job cobraba con la wallet
     preferida (`IGatewayResolver`, p. ej. eWallet) sin candado ni estado. El motor no tiene esa ruta: esas
     renovaciones ya solo se pagan con comisiones (saldo gastable) o fallan con `CARD_DISABLED`.
  3. **Los 33 estados `Stopped` con la membresía `Active` de R13**: el job los renovaba por su cuenta; el
     motor no los mira. Siguen activos sin pagar hasta que Nick decida (consulta de §12).

  ```sql
  -- Activas que renovaba el job y no tienen estado vivo en el motor (o lo tienen parado):
  SELECT m.MembershipLevelId, COUNT(*) AS suscripciones
  FROM MembershipSubscriptions m WITH (NOLOCK)
  LEFT JOIN SubscriptionBillingStates s WITH (NOLOCK) ON s.MembershipSubscriptionId = m.Id AND s.Purpose = 1
  WHERE m.SubscriptionStatus = 1 AND m.IsAutoRenew = 1 AND m.CancellationDate IS NULL AND m.IsFree = 0
    AND m.IsDeleted = 0 AND (s.Id IS NULL OR s.Status = 4)
  GROUP BY m.MembershipLevelId;
  ```

  En local (2026-10-17): 33, todas de nivel 3 (son las de R13); ninguna de nivel 1 ni sin estado.
  Resultado en producción: ________.
- **«Calificado» con la anualidad real**: los Ambassadors con más de 12 meses cuyo estado de cuota vence
  en el futuro pasan a «Calificado» (P1); los que tienen el ciclo vencido sin pagar, no. Avisar a
  soporte. En local solo hay 2 Ambassadors con más de un año.
- **Pendiente fuera de este plan**: índice de `Orders.OrderNo` (R11), `bill-now` del Admin sin registrar
  (R4), `POST billing/memberships/renew` (P4), token sin consumir de `FundWithCommissionBalanceAsync` (R8).

### Pruebas

Preparación: SignupAPI (7005), Billing, BizCenter (7003), BizCenterWeb (7004), AdminAPI (7002) y
AdminWeb (7001) arrancados con el perfil `https` contra la base local, con `CuotaAnual` aplicada y
`annual-fee-state-enrollment` lanzado a mano desde `/hangfire`. Dos Ambassadors A y B; A con 150 de comisiones pagables y un token
«Annual: Biz Center» (comprado en `/tokens` o sembrado en SQL); su estado de cuota puesto a vencer hoy
(`UPDATE SubscriptionBillingStates SET NextBillingDate = CAST(GETDATE() AS date), NextAttemptDate = CAST(GETDATE() AS date) WHERE Purpose = 2 AND MemberId = @A`).
`TOKEN_A` es el Bearer de A y `PERMISO_A` un permiso de step-up de `member.annual-fee.pay-from-balance`
emitido para A.

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 13.1 | IDOR en el resumen | `GET /api/v1/bizcenter/billing/annual-fee?memberId={B}` con `TOKEN_A` | el resumen de A; el parámetro se ignora | ☐ |
| 13.2 | IDOR al pagar | `POST …/annual-fee/pay` con `TOKEN_A`, `PERMISO_A` y `"memberId":"{B}"` en el cuerpo | paga (o rechaza) la cuota de **A**; la de B no cambia | ☐ |
| 13.3 | Sin step-up | `POST …/annual-fee/pay` con `TOKEN_A` y sin cabecera | `403 STEP_UP_REQUIRED`; nada en `AnnualFeePayments` | ☐ |
| 13.4 | Permiso de otra operación | el mismo pago con un permiso de `member.tokens.purchase-from-balance` | `403 STEP_UP_INVALID` (BizCenter) y ningún pago | ☐ |
| 13.5 | Permiso repetido | dos pagos con claves distintas y el mismo `PERMISO_A` | el segundo, `403 STEP_UP_INVALID` | ☐ |
| 13.6 | Billing directo | `POST https://<billing>/api/v1/billing/annual-fee/pay` con `TOKEN_A`: sin permiso; con un permiso ya usado en el BizCenter | `403 STEP_UP_REQUIRED`; el segundo pasa una vez y la repetición con otra clave da `403 STEP_UP_INVALID` | ☐ |
| 13.7 | Suplantación completa | un administrador suplanta a A (token completo) y llama a `pay` y a `preference` | `403 IMPERSONATION_CANNOT_PAY_ANNUAL_FEE` en BizCenter y en Billing; la tarjeta del perfil sale de solo lectura | ☐ |
| 13.8 | Suplantación de solo lectura | lo mismo con token de solo lectura | `403 IMPERSONATION_READ_ONLY` (middleware); el `GET` funciona | ☐ |
| 13.9 | Tarjeta | `pay` con `"method":"CreditCard"` y con `"Crypto"`, y con `"1"` | `400 ANNUAL_FEE_METHOD_NOT_SUPPORTED`; Billing no recibe nada | ☐ |
| 13.10 | Idempotencia | dos `pay` con la misma clave y el mismo método (dos permisos) | el mismo `paymentId`; un solo pedido y un solo débito | ☐ |
| 13.11 | Clave reutilizada | la misma clave con otro método | `409 IDEMPOTENCY_KEY_REUSED` | ☐ |
| 13.12 | Doble pago del ciclo | dos `pay` a la vez con claves distintas (dos pestañas) | uno `200`; el otro `400 ANNUAL_FEE_NOT_DUE` o `409 ANNUAL_FEE_ALREADY_PAID`; un solo pago `Completed` | ☐ |
| 13.13 | Ventana de pago | `pay` con el estado venciendo dentro de 31 días | `400 ANNUAL_FEE_NOT_DUE` | ☐ |
| 13.14 | `KeepQualified` | `UPDATE MemberProfiles SET KeepQualified = 1 WHERE MemberId = @A` y `pay` | `400 ANNUAL_FEE_WAIVED`; la tarjeta dice «Exenta» | ☐ |
| 13.15 | Conciliar sin rol | `POST …/annual-fee/payments/{id}/reconcile` con un token de soporte (`SupportLevel1`) y con el de A | `403` en los dos | ☐ |
| 13.16 | Billing a través de AdminAPI | `POST https://localhost:7002/api/v1/billing/annual-fee/pay` con `TOKEN_A` | `404`, `401` o `500` sin efectos: nada en `AnnualFeePayments` (R4) | ☐ |
| 13.17 | Cobro automático apagado | con el interruptor en `false`, lanzar `recurring-billing-sweep` desde Hangfire | los estados de cuota que vencen hoy no se cobran; ni pedidos ni pagos nuevos | ☐ |
| 13.18 | Exclusiones | con el interruptor en `true` y un estado vencido por cada caso (cuenta `Terminated`, `Suspended`, membresía en `HoldByBilling`, membresía cancelada —`Cancelled` o con la cancelación programada cumplida—, inscrita hace menos de un año, `KeepQualified`, cobro hace 2 meses), lanzar el barrido | `Stopped` / mañana (`ACCOUNT_SUSPENDED`) / mañana (`MEMBERSHIP_ON_HOLD`) / mañana (`MEMBERSHIP_CANCELLED`) / aniversario / mañana (`KEEP_QUALIFIED`) / a un año del cobro; ningún cobro ni intento en `RecurringBillingAttempts`; con la membresía cancelada, «Pagar ahora» sigue disponible | ☐ |
| 13.19 | Orden de cobro | A con token y comisiones, el interruptor encendido, barrido | se gasta el token (`TokenTransactions` `Used` con el pedido); las comisiones no se tocan | ☐ |
| 13.20 | Preferencia apagada | A sin token, con comisiones y `FeesFromBalance = 0`, barrido | intento `Failed` (`ANNUAL_FEE_NO_FUNDS`), sin débito; la membresía y `MemberProfiles.Status` no cambian | ☐ |
| 13.21 | Un solo motor | `/hangfire` → *Recurring jobs* | no está `membership-auto-renewal`; sí `recurring-billing-planning`, `recurring-billing-sweep`, `annual-fee-stale-pending` y `annual-fee-state-enrollment` (este con «Never» mientras `EnrollmentJobEnabled` no esté a `true`) | ☐ |
| 13.22 | Tarjeta apagada y stub | una renovación de Travel Advantage sin comisiones: con `Billing:Recurring:CardEnabled` sin poner; después en `true` con el gateway de prueba | apagada: intento `Failed` con `CARD_DISABLED`, cadencia normal, pedido `Cancelled`, sin `PaymentHistory` y sin llamar al gateway. Encendida con el stub: intento `Failed` con `CARD_SIMULATED_CHARGE` (log `Critical`), cadencia normal, sin renovar | ☐ |
| 13.23 | Candado en Travel Advantage | una compra de tokens con comisiones de A mientras el barrido renueva su Travel Advantage | nunca se gasta más de lo gastable; una de las dos espera o devuelve ocupado | ☐ |
| 13.24 | Admin solo lectura | perfil de A en AdminWeb | la tarjeta sin botones ni interruptor; `GET api/v1/admin/members/{A}/billing/annual-fee` responde; no hay `POST`/`PUT` en esa ruta | ☐ |
| 13.25 | Hora de servidor | cambiar la zona horaria del navegador a UTC+14 y abrir el perfil | el estado y el vencimiento no cambian | ☐ |
