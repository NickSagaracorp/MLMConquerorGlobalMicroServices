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
| Correo y SMS | proveedor `Null` | Los códigos van al log. Encender SES es un paso de despliegue con prerrequisitos |
| Step-up en operaciones críticas | sin construir | Es la segunda mitad del encargo original. Hoy el segundo factor se verifica al entrar, no en el momento de cada operación sensible |
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
