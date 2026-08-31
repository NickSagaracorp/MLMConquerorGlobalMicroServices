# Llaves JWT en desarrollo — dónde está la privada y cómo se configura

**Fecha:** 2026-08-30
**Afecta a:** SignupAPI y AdminAPI (los dos únicos servicios que **firman** tokens)
**Complementa a:** [`rotacion-llaves-jwt.md`](rotacion-llaves-jwt.md), que cubre producción

---

## 1. Qué cambió

La llave privada RSA de desarrollo estaba escrita byte a byte en
`appsettings.Development.json` de AdminAPI y de SignupAPI. **Ya no.** Ahora vive en el
almacén de secretos de usuario de .NET, fuera del repositorio.

### Qué no era el problema

Conviene decirlo porque cambia la urgencia: **ese archivo nunca llegó a git.**
`.gitignore` lo excluye desde su línea 99 (`appsettings.*.json`, con
`!appsettings.json` justo debajo), y `git log --all -- <ruta>` no devuelve ni un commit.
Esta llave **no** es la llave revocada de la que habla `rotacion-llaves-jwt.md`; aquella
sí estaba commiteada, en `appsettings.json`, y `JwtKeyGuard` la rechaza hoy por huella.

### Qué sí era el problema

La llave estaba en **texto plano dentro del árbol de trabajo**, y de ahí sale sola por
más caminos de los que parece:

- MSBuild la copiaba a `bin/Debug/net10.0/appsettings.Development.json` en cada
  compilación — de AdminAPI, de SignupAPI y de sus dos proyectos de pruebas: cuatro
  copias más que nadie estaba mirando.
- Un `git add -f` de alguien con prisa la commitea, y a partir de ahí es permanente.
- Un zip de la carpeta, un respaldo del disco, una copia a otra máquina «para probar» o
  una captura de pantalla del editor la sacan del ordenador sin dejar rastro.

El secreto que firma las credenciales de la casa no tiene por qué estar en el directorio
del código. Sacarlo cuesta un minuto y elimina los cuatro caminos de golpe.

**Esto no es una rotación.** La llave es la misma; solo cambió de sitio. No hay que tocar
producción, ni la base de datos, ni las sesiones abiertas.

---

## 2. Dónde está ahora

```
%APPDATA%\Microsoft\UserSecrets\mlmconqueror-jwt-signers\secrets.json
```

`WebApplication.CreateBuilder` carga ese archivo automáticamente **y solo cuando
`ASPNETCORE_ENVIRONMENT` es `Development`**. No hay que añadir nada al `Program.cs`.

### Por qué los dos servicios comparten un mismo `UserSecretsId`

AdminAPI y SignupAPI tienen que firmar con **la misma** llave: si no, un token emitido
por uno no vale en el otro y medio sistema devuelve 401. Compartir el almacén convierte
esa regla en algo estructural —no hay dos valores que puedan divergir— en vez de una nota
que hay que acordarse de cumplir dos veces.

Los otros cinco servicios (BizCenter, RankEngine, TicketManagementSystem, Billing,
CommissionEngine) **no llevan `UserSecretsId`**: solo verifican, y la llave pública no es
secreta. Sigue en su `appsettings.json`, commiteada, como hasta ahora.

---

## 3. Configurar una máquina nueva

Sin este paso, AdminAPI y SignupAPI **se niegan a arrancar** con este mensaje:

> `Jwt:PrivateKeyBase64 no está configurada. En desarrollo va en appsettings.Development.json;
> en producción, en appsettings.Production.json. Plantilla: docs/deployment/jwt-keys.template.json.`

Es `JwtKeyGuard` haciendo su trabajo: falla al arrancar y no a mitad de un login.

### Opción A — generar tu propio par (recomendada)

Cada desarrollador con su propia llave es lo más limpio: no hay ningún secreto que pasarse
por chat. El precio es que también tienes que poner **tu** llave pública en los siete
servicios, porque el par tiene que cuadrar.

```bash
# 1. Generar el par
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER -out priv.der
openssl rsa -in priv.der -inform DER -pubout -outform DER -out pub.der
```

```powershell
# 2. Pasarlo a base64 (PowerShell, si no tienes `base64`)
$priv = [Convert]::ToBase64String([IO.File]::ReadAllBytes("priv.der"))
$pub  = [Convert]::ToBase64String([IO.File]::ReadAllBytes("pub.der"))
```

```bash
# 3. La privada, al almacén de secretos. Basta hacerlo UNA vez:
#    el otro firmante comparte el mismo almacén.
dotnet user-secrets set "Jwt:PrivateKeyBase64" "<contenido de priv>" \
  --project MLMConquerorGlobalEdition.AdminAPI
```

4. La pública va en `Jwt:PublicKeyBase64` de los **siete** `appsettings.json`. Como esos
   archivos sí están commiteados, no los guardes con tu llave: ponla en el
   `appsettings.Development.json` de cada servicio, que está excluido de git.

5. Borra `priv.der` y cualquier archivo temporal con la privada.

### Opción B — usar la llave de desarrollo compartida del equipo

Si el equipo comparte un par de desarrollo (que es lo que hay hoy: la pública de ese par
está en los siete `appsettings.json`), pide la privada a alguien que ya la tenga y:

```bash
dotnet user-secrets set "Jwt:PrivateKeyBase64" "<la cadena base64>" \
  --project MLMConquerorGlobalEdition.AdminAPI
```

**Pásala por un canal que no la archive** —un gestor de contraseñas compartido, no un
chat ni un correo—. Y no vuelvas a escribirla en ningún archivo del repositorio: si
aparece otra vez en un `appsettings.*.json`, hemos vuelto al punto de partida.

### Comprobar que quedó bien

```bash
# Debe listar Jwt:PrivateKeyBase64. Ojo: imprime el valor.
dotnet user-secrets list --project MLMConquerorGlobalEdition.AdminAPI

# El mismo comando desde el otro firmante devuelve LO MISMO: comparten almacén.
dotnet user-secrets list --project MLMConquerorGlobalEdition.SignupAPI
```

Y luego, la de verdad: arrancar los dos servicios y hacer un login. Si arrancan sin el
error de arriba, la llave se está leyendo.

---

## 4. Formato de la llave — los tres errores de siempre

`JwtKeyGuard` valida al arrancar y el mensaje dice siempre qué pasa. Los tres casos:

| Mensaje | Qué pasó |
|---|---|
| «está en formato PKCS#1» | Usaste `openssl genrsa`. Este repositorio usa PKCS#8, que es lo que da `openssl genpkey`. El propio mensaje trae el comando para convertirla |
| «no es una llave RSA válida» | Pegaste el `.pem` con sus cabeceras `-----BEGIN...-----`, o la cadena se cortó al copiar, o confundiste la pública con la privada |
| «contiene la llave revocada» | Se coló la llave vieja, la que sigue en el historial de git. Genera un par nuevo |

---

## 5. Lo que sigue pendiente

**Producción no está resuelta por esto.** Allí la llave sigue en
`appsettings.Production.json`, en el disco del servidor y en texto plano. La sección 8 de
`rotacion-llaves-jwt.md` ya proponía evaluar AWS Secrets Manager, que es donde esto
termina. Este cambio solo saca el secreto del directorio de trabajo de los
desarrolladores.

**El almacén de secretos tampoco cifra nada.** `secrets.json` es texto plano, protegido
solo por los permisos del perfil de usuario de Windows. Lo que gana es que está **fuera
del repositorio**: no se commitea por accidente, no se copia a `bin/`, y no viaja en un
zip del proyecto. Para desarrollo eso es exactamente lo que hacía falta; para producción,
no basta.
