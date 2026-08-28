# Verificación del 2FA de administración

**Fecha:** 2026-08-28
**Rama:** `feature/plan-b-authn-notifications`
**Estado:** migraciones aplicadas en desarrollo; pendiente de verificación manual

---

## 1. Qué se ha construido

Tres bloques de trabajo, 1565 pruebas automatizadas en verde:

| Bloque | Qué entrega |
|---|---|
| **Prerrequisitos de seguridad** | Llave RSA fuera del repositorio, bloqueo por intentos fallidos funcionando, autenticación de Billing y CommissionEngine reparada |
| **Núcleo de dos factores** | Librería que emite y verifica códigos por tres canales, con límites y antirreplay; transportes de correo (SES) y SMS (Twilio) |
| **Login con dos factores** | El administrador entra con código; enrolamiento forzado; pantallas de verificación y alta |

**Nada de esto está activo todavía.** La configuración por defecto deja el sistema comportándose exactamente como antes:

- `Auth:TwoFactor:MandatoryRoles` está **vacío** → nadie es forzado a enrolarse
- `Notifications:Email:Provider` y `Notifications:Sms:Provider` están en **`Null`** → los códigos se escriben en el log, no se envían

Esa es la posición de partida deliberada: el código viaja desplegado y dormido, y se enciende cuando se haya comprobado que funciona.

---

## 2. Estado de la base de datos de desarrollo

Las dos migraciones ya están aplicadas y verificadas:

| Comprobación | Resultado |
|---|---|
| Columnas nuevas en `AspNetUsers` | 5 |
| Usuarios con canal preferido = correo | 119.386 |
| Usuarios con cualquier otro canal | 0 |
| Tablas nuevas | 4 |
| Plantillas de correo y SMS (inglés y español) | 1 + 1, con 2 localizaciones cada una |

Que los 119.386 usuarios quedaran en **correo** y ninguno en otro canal es lo esperado y lo importante: el valor cero del enumerado es «aplicación de autenticación», y sin el relleno explícito de la migración todos habrían quedado apuntando a un canal que nunca configuraron.

**Dato operativo que conviene conocer:** los ocho servicios ejecutan `MigrateAsync()` al arrancar. **Desplegar cualquiera de ellos aplica automáticamente las migraciones pendientes**, sin paso ni aprobación intermedia. No es algo introducido por este trabajo —ya era así—, pero cambia cómo hay que planificar un despliegue con migraciones grandes.

---

## 3. Verificaciones, en orden

Hazlas en este orden: cada una supone que la anterior pasó.

### Antes de empezar

**Usa el perfil HTTPS.** Las cookies del proceso de verificación llevan `Secure=true` y un navegador no las guarda sobre HTTP plano. Si se prueba sobre `http://`, el código parecerá perderse entre pantallas y el problema será el protocolo, no el 2FA.

Servicios necesarios: **SignupAPI** (autenticación) y **AdminWeb** (portal).

---

### 3.1 — Que nada cambió

**La más importante de todas.** Con la configuración por defecto, entra al portal con un administrador que **no** tenga 2FA.

- **Esperado:** entra directamente, como siempre. Ni pantalla de código ni de enrolamiento.
- **Si falla:** para aquí. Significa que el trabajo alteró el login de quien no usa dos factores, que es la regresión más cara posible.

Comprueba también que un miembro del BizCenter puede entrar. `SignupAPI` sirve a los dos portales, y el cambio los toca a los dos.

---

### 3.2 — El bloqueo por intentos fallidos

Con una cuenta de prueba, falla la contraseña **cinco veces seguidas**.

- **Esperado:** al sexto intento responde que la cuenta está bloqueada, **incluso con la contraseña correcta**. El bloqueo dura 15 minutos.
- **Por qué importa:** esto estaba configurado desde el principio pero nunca se invocaba. El login real no tenía freno de fuerza bruta.
- **Para desbloquear antes:** poner `LockoutEnd` a `NULL` en `AspNetUsers` para esa cuenta.

---

### 3.3 — La aplicación de autenticación, de punta a punta

Es el único canal que se puede verificar completo sin credenciales externas.

1. A un usuario de prueba, poner en base de datos `TwoFactorEnabled = 1` y `PreferredTwoFactorChannel = 0` (aplicación de autenticación)
2. Intentar entrar

- **Esperado:** el portal pide un código de seis dígitos, **sin** botón de reenviar —no hay nada que reenviar, el código lo genera la aplicación— y sin mostrar ningún destino.
- **Problema esperable:** el usuario todavía no tiene una clave enrolada, así que el sistema responderá que el canal no está disponible. Es correcto: primero hay que enrolarse (3.4).

---

### 3.4 — El enrolamiento forzado

1. En `appsettings.Development.json` de SignupAPI, poner el rol del usuario de prueba en `Auth:TwoFactor:MandatoryRoles`, por ejemplo `["Admin"]`
2. Quitarle el 2FA: `TwoFactorEnabled = 0`
3. Reiniciar SignupAPI y entrar

- **Esperado:** tras la contraseña correcta, va a la pantalla de enrolamiento con un código QR y la clave en texto.
- **Comprobación clave:** intenta navegar a cualquier otra ruta del portal, por ejemplo `/admin` o `/admin/members`. **No debe dejarte.** El token de enrolamiento no es un token de acceso y no abre ningún endpoint de negocio.
- Escanea el QR con Google Authenticator, Microsoft Authenticator o 1Password, e introduce el número de seis dígitos.
- **Esperado:** entra al portal directamente, sin volver a pedir contraseña.

**Prueba adicional que merece la pena:** escanea el QR, luego introduce un código **incorrecto** a propósito, y cuando vuelva a cargar la pantalla introduce el código correcto de la entrada que ya tienes en la aplicación.

- **Esperado:** funciona. La clave **no** se regenera entre intentos.
- **Por qué:** hasta hace poco sí se regeneraba, y eso creaba una trampa sin salida — el usuario tecleaba el número de una entrada que ya no valía, fallaba, y así indefinidamente hasta agotar los intentos.

---

### 3.5 — El canal correo, sin enviar nada

Prueba la cadena completa —plantilla, sustitución de variables, emisión del reto, verificación— sin necesitar SES.

1. Con el usuario ya enrolado, cambiar `PreferredTwoFactorChannel = 1` (correo)
2. Entrar

- **Esperado:** la pantalla dice que se envió un código al correo, con la dirección enmascarada (`n***@dominio.com`).
- **El código está en el log de SignupAPI.** Busca `NullEmailService` o `TWO_FACTOR_CODE`. Cópialo e introdúcelo.
- **Esperado:** entra.
- **Si el log dice que falta la plantilla**, nombrará el `eventType` que no encontró. Las plantillas están sembradas en inglés y español; cualquier otro idioma cae a inglés.

---

### 3.6 — Los límites

Con el canal correo activo:

**Intentos por código:** pide un código e introduce cinco códigos incorrectos.
- **Esperado:** el sexto responde «demasiados intentos», **aunque sea el correcto**. Hay que pedir un código nuevo.

**Códigos por ventana:** pide cuatro códigos seguidos.
- **Esperado:** el cuarto responde que se han pedido demasiados. La ventana es de 15 minutos.
- **Por qué importa:** sin este límite, quien conozca un correo puede provocar envíos de SMS ilimitados a costa de la empresa, porque Twilio cobra por mensaje.

---

### 3.7 — Volver al estado inicial

Al terminar, deja la configuración como estaba:

- `Auth:TwoFactor:MandatoryRoles` de nuevo **vacío**
- `TwoFactorEnabled = 0` en las cuentas de prueba

---

## 4. Qué no se puede verificar todavía

**La entrega real de correos y SMS.** Todas las pruebas automatizadas aíslan la llamada de red, y los proveedores están en `Null`. Que un SMS llegue de verdad al teléfono solo se comprueba con credenciales de Twilio y una cuenta de SES fuera del entorno de pruebas.

Cuando existan esas credenciales, la secuencia es:

1. Ponerlas en `appsettings.Production.json`, que está fuera de git
2. Cambiar los proveedores a `Ses` y `Twilio`
3. Repetir 3.5 con un correo real, y añadir un teléfono a una cuenta de prueba para el canal SMS
4. **Solo entonces** llenar `MandatoryRoles`

Ese orden importa: si se hace obligatorio el 2FA antes de comprobar que los transportes entregan, un administrador cuyo canal sea el correo se queda sin poder entrar en cuanto falle el envío.

---

## 5. Riesgos abiertos

**La llave RSA anterior sigue en el historial de git.** Se rotó y el sistema rechaza la vieja por huella criptográfica, pero cualquiera con un clon antiguo la tiene. Borrarla del historial exige reescribirlo y coordinar con todos los clones existentes.

**Las traducciones de la interfaz no están revisadas.** Los textos de las pantallas nuevas se tradujeron a los nueve idiomas siguiendo la terminología ya presente en el proyecto, pero **ningún hablante nativo los ha revisado**, en particular georgiano y coreano.

**Las plantillas de correo y SMS solo existen en inglés y español.** Los otros siete idiomas caen a inglés. Es deliberado: una traducción sin revisar de un correo de seguridad parece revisada y no lo está.

**El SMS no lleva el aviso de seguridad** que sí lleva el correo. Es una decisión de coste: los caracteres acentuados del español fuerzan una codificación de 70 caracteres por segmento, y añadir el aviso duplicaría el precio de cada inicio de sesión legítimo.

**Billing y CommissionEngine** tenían la autenticación rota —verificaban firmas RSA con una llave de otro tipo— y se reparó. Sus endpoints HTTP no los usaba ningún cliente, así que el negocio no estaba afectado, pero conviene comprobar que siguen funcionando si algo empieza a consumirlos.

---

## 6. Después de verificar

Si todo pasa, lo siguiente en orden de valor:

1. **Credenciales de SES y Twilio**, y repetir la verificación de los canales correo y SMS
2. **Rotar la llave RSA en producción**, siguiendo la guía de despliegue
3. **Llenar `MandatoryRoles`** y anunciar al equipo que tendrán que enrolarse
4. **El resto de la superficie de cuenta**: recuperación de contraseña, confirmación de correo, alta de teléfono, gestión del 2FA desde el perfil
5. **La confirmación por código en operaciones críticas**, que era el segundo objetivo del encargo original
