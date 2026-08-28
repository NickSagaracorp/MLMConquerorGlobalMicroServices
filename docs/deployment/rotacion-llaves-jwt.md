# Rotación de llaves JWT — Guía de despliegue

**Fecha:** 2026-08-28
**Afecta a:** los 7 servicios de MLMConqueror Global Edition
**Requiere:** ventana de mantenimiento
**Tiempo estimado:** 45–60 minutos

---

## 1. Por qué hay que hacer esto

La llave privada RSA que firma todos los tokens de autenticación del sistema estaba
**commiteada en texto plano** dentro del repositorio, en `appsettings.json` de AdminAPI y
SignupAPI.

Con esa llave, cualquiera que tuviera acceso al repositorio —incluido cualquier clon, fork
o copia de respaldo— podía firmar un token de acceso con rol `SuperAdmin` **sin conocer
ninguna contraseña**. No hacía falta explotar nada: bastaba con generar el token.

La llave ya fue reemplazada en el código. Falta hacer lo mismo en producción.

**La llave anterior sigue en el historial de git y no se puede recuperar la confianza en
ella.** Aunque se borre del código actual, cualquiera con un clon antiguo la tiene. Por eso
el sistema ahora la rechaza activamente: si alguien intenta arrancar un servicio con esa
llave, el servicio se niega a iniciar.

---

## 2. Qué cambia para los usuarios

| Cambio | Efecto |
|---|---|
| Rotación de la llave | **Todas las sesiones activas se cierran.** Todo el mundo vuelve a iniciar sesión |
| Bloqueo por intentos fallidos | 5 contraseñas incorrectas bloquean la cuenta 15 minutos. Antes estaba configurado pero no funcionaba |
| APIs de Billing y CommissionEngine | Pasan a aceptar tokens. Antes rechazaban todo con error 401 |

El cierre de sesiones es inevitable: los tokens emitidos con la llave vieja dejan de ser
válidos en el momento en que los servicios pasan a la llave nueva.

---

## 3. Antes de empezar

**Necesitas:**

- Acceso al servidor o al pipeline de despliegue de los 7 servicios
- OpenSSL disponible (viene con Git para Windows, o `winget install ShiningLight.OpenSSL`)
- Una ventana de mantenimiento acordada, fuera de horario de alta actividad
- Confirmar quién avisa a los usuarios de que tendrán que volver a entrar

**No necesitas:** cambios en base de datos. Esta rotación no toca ningún dato.

**Advertencia sobre los archivos de configuración:** los `appsettings.*.json` están
excluidos de git a propósito. **Nunca los commitees con valores reales.** Si tu pipeline los
genera, revisa que la llave privada llegue por variable de entorno o gestor de secretos, no
por un archivo versionado.

---

## 4. Qué llave necesita cada servicio

Solo dos servicios **firman** tokens; los otros cinco solo los **verifican**. La llave
privada firma; la pública verifica. La pública no es secreta.

| Servicio | Llave privada | Llave pública |
|---|---|---|
| SignupAPI | **Sí** | Sí |
| AdminAPI | **Sí** | Sí |
| BizCenter | No | Sí |
| RankEngine | No | Sí |
| TicketManagementSystem | No | Sí |
| Billing | No | Sí |
| CommissionEngine | No | Sí |

**La llave privada debe ser exactamente la misma en SignupAPI y AdminAPI**, y la pública
exactamente la misma en los siete. Son un par: si no corresponden entre sí, nada funciona.

---

## 5. Paso a paso

### Paso 1 — Generar el par de llaves nuevo

En cualquier máquina con OpenSSL, en un directorio temporal:

```bash
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER -out priv.der
openssl rsa -in priv.der -inform DER -pubout -outform DER -out pub.der
base64 -w0 priv.der > priv.txt
base64 -w0 pub.der  > pub.txt
```

En PowerShell, si `base64` no está disponible:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("priv.der")) | Set-Content priv.txt -NoNewline
[Convert]::ToBase64String([IO.File]::ReadAllBytes("pub.der"))  | Set-Content pub.txt -NoNewline
```

Ahora tienes dos cadenas:

- `priv.txt` → el contenido va en **PrivateKeyBase64** (secreto)
- `pub.txt` → el contenido va en **PublicKeyBase64** (no secreto)

**Importante sobre el formato.** Debe ser **PKCS#8**, que es lo que produce `openssl
genpkey`. Si usas `openssl genrsa` obtendrás PKCS#1 y el servicio lo rechazará al arrancar
—aunque con un mensaje que te dice cómo convertirlo—. Tampoco pegues el contenido de un
archivo `.pem` con sus cabeceras `-----BEGIN ...-----`: se necesita solo el base64 del DER.

**Borra `priv.der` y `priv.txt` en cuanto hayas colocado la llave.** No los dejes en el
disco ni los envíes por chat o correo.

### Paso 2 — Preparar la configuración de cada servicio

Para **SignupAPI** y **AdminAPI**, en su `appsettings.Production.json`:

```json
{
  "Jwt": {
    "PrivateKeyBase64": "<contenido de priv.txt>",
    "PublicKeyBase64": "<contenido de pub.txt>",
    "Issuer": "MLMConquerorGlobalEdition",
    "Audience": "MLMConquerorGlobalEdition.Clients"
  }
}
```

Para **BizCenter**, **RankEngine**, **TicketManagementSystem**, **Billing** y
**CommissionEngine**, lo mismo pero **sin** `PrivateKeyBase64`:

```json
{
  "Jwt": {
    "PublicKeyBase64": "<contenido de pub.txt>",
    "Issuer": "MLMConquerorGlobalEdition",
    "Audience": "MLMConquerorGlobalEdition.Clients"
  }
}
```

`Issuer` y `Audience` deben ser **idénticos en los siete servicios**, con el punto en
`MLMConquerorGlobalEdition.Clients`. Un servicio con la audiencia mal escrita rechaza todos
los tokens.

Si tu despliegue usa variables de entorno en lugar de archivos, los nombres son
`Jwt__PrivateKeyBase64` y `Jwt__PublicKeyBase64` (doble guion bajo).

### Paso 3 — Desplegar

**No hay forma de hacer esto sin corte.** Los servicios no admiten dos llaves a la vez, así
que durante la transición habrá un momento en que unos validen con la llave nueva y otros
firmen con la vieja. Por eso se hace en ventana de mantenimiento y todo junto.

1. Anuncia el inicio de la ventana
2. **Detén los 7 servicios**
3. Aplica la configuración del Paso 2 en los 7
4. **Arranca primero SignupAPI y AdminAPI.** Si alguno se niega a arrancar, para aquí y
   mira la sección 7: no continúes hasta resolverlo
5. Arranca los cinco restantes
6. Ejecuta la verificación del Paso 4
7. Anuncia el fin de la ventana

Arrancar primero los que firman permite detectar un problema con la llave privada antes de
tocar el resto.

### Paso 4 — Verificar

**4.1 — Los servicios arrancaron.** Revisa los logs de los 7. Un arranque correcto muestra
`Application started`. Si un servicio se negó a arrancar, el log lleva un mensaje explícito
que dice qué falta; ve a la sección 7.

**4.2 — El login funciona.** Entra al portal de administración con una cuenta real. Debe
pedirte credenciales de nuevo, aunque tuvieras sesión antes: eso confirma que la llave
cambió.

**4.3 — El token sirve en todos los servicios.** Con la sesión iniciada, comprueba que
funcionan pantallas que consultan servicios distintos: comisiones, rangos, tickets y
facturación. Si una pantalla devuelve 401 y las otras no, ese servicio tiene la llave
pública o la audiencia mal.

**4.4 — El bloqueo por intentos funciona.** Con una cuenta de prueba, falla la contraseña 5
veces. Al sexto intento debe responder que la cuenta está bloqueada, incluso con la
contraseña correcta. Espera 15 minutos o desbloquéala desde la base de datos.

**4.5 — La llave vieja ya no sirve.** Si tienes guardado un token anterior a la rotación,
compruébalo contra cualquier endpoint: debe devolver 401.

---

## 6. Si algo sale mal: volver atrás

**No se puede volver a la llave anterior.** El sistema la rechaza a propósito, por huella
criptográfica, precisamente para que nadie la reintroduzca desde un respaldo o un commit
viejo.

Si la rotación falla, la salida es hacia adelante:

1. Genera **otro** par nuevo con el Paso 1
2. Repite desde el Paso 2

Si necesitas restaurar el servicio con urgencia y sospechas de la configuración, lo más
rápido es volver a copiar el mismo par en los 7 servicios con cuidado, verificando que la
cadena se pegó completa —el error más común es una llave truncada al copiar—.

---

## 7. Solución de problemas

Los mensajes aparecen en el log del servicio al arrancar y dicen siempre qué clave de
configuración es la del problema.

### «...no está configurada»

> `Jwt:PrivateKeyBase64 no está configurada. En desarrollo va en appsettings.Development.json;
> en producción, en appsettings.Production.json.`

Falta el valor, o está vacío, o el archivo no llegó al servidor. Comprueba que el
`appsettings.Production.json` existe en el directorio del servicio y que el entorno está
en `Production` (`ASPNETCORE_ENVIRONMENT`).

### «...contiene la llave revocada»

> `Jwt:PrivateKeyBase64 contiene la llave revocada que sigue commiteada en el repositorio.`

Se coló la llave vieja. Suele pasar cuando un script de despliegue copia el
`appsettings.json` del repositorio encima del de producción, o cuando alguien restaura un
respaldo previo. Genera un par nuevo y revisa el pipeline.

### «...está en formato PKCS#1»

> `Jwt:PrivateKeyBase64 está en formato PKCS#1 (la llave de origen tendría la cabecera
> '-----BEGIN RSA PRIVATE KEY-----'). Este repositorio usa PKCS#8.`

Generaste la llave con `openssl genrsa`. Conviértela:

```bash
openssl pkcs8 -topk8 -nocrypt -in llave.pem -out llave-pkcs8.pem
```

y vuelve a extraer el DER en base64. O simplemente genera el par de nuevo con `openssl
genpkey`, como indica el Paso 1.

### «...tiene N bits; el mínimo es 2048»

La llave es demasiado corta para ser segura. Regenera con `rsa_keygen_bits:2048` o superior.

### «...no es una llave RSA válida»

El valor no es base64 de una llave RSA. Casi siempre es una de estas tres:

- La cadena se cortó al copiar (revisa que termine igual que el archivo original)
- Se pegó el contenido del `.pem` con las cabeceras `-----BEGIN...-----`
- Se confundió la pública con la privada, o al revés

### Los servicios arrancan pero el login devuelve 401 en todo

La pública no corresponde a la privada. Asegúrate de que `pub.txt` y `priv.txt` salieron
del **mismo** `openssl genpkey`, y de que copiaste la pública a los 7 servicios.

### Una sola pantalla devuelve 401 y el resto funciona

Ese servicio concreto tiene mal la llave pública o la audiencia. Revisa que su `Audience`
sea `MLMConquerorGlobalEdition.Clients`, **con el punto**.

---

## 8. Después del despliegue

Dos cosas quedan pendientes y conviene decidirlas pronto:

**La llave vieja sigue en el historial de git.** El sistema la rechaza, así que no puede
reutilizarse en estos servicios, pero cualquiera con un clon la tiene. Borrarla del
historial requiere reescribirlo (`git filter-repo`) y forzar el push, coordinando con todos
los clones existentes. Es una operación disruptiva que merece su propia planificación.

**Cómo se guardan los secretos.** Ahora mismo la llave privada vive en un archivo de
configuración excluido de git. Funciona, pero depende de que nadie cambie esa exclusión.
Vale la pena evaluar un gestor de secretos —AWS Secrets Manager, dado que ya se usa AWS— para
que la llave no esté en el disco del servidor en texto plano.

---

## Anexo — Resumen en una página

| | |
|---|---|
| **Generar** | `openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER -out priv.der` |
| | `openssl rsa -in priv.der -inform DER -pubout -outform DER -out pub.der` |
| **Privada en** | SignupAPI, AdminAPI |
| **Pública en** | los 7 servicios |
| **Issuer** | `MLMConquerorGlobalEdition` |
| **Audience** | `MLMConquerorGlobalEdition.Clients` |
| **Archivo** | `appsettings.Production.json`, nunca commiteado |
| **Orden** | detener todo → configurar → arrancar SignupAPI y AdminAPI → arrancar el resto |
| **Efecto** | todas las sesiones se cierran |
| **Rollback** | no existe: la llave vieja está revocada. Se genera otro par |
