# Secretos desde AWS Secrets Manager, por servicio y por entorno — Diseño

**Fecha:** 2026-11-19
**Estado:** aprobado por secciones; pendiente de revisión del documento completo
**Rama:** `secretos-aws`

## 1. Objetivo

Que ningún secreto de los 11 servicios salga de `appsettings.json`, de un `.env` o de una variable de entorno escrita a mano. Cada servicio lee de AWS Secrets Manager, al arrancar, **solo los secretos que necesita**, y su identidad en AWS **solo puede leer esos**.

Aplica a tres entornos (`local`, `staging`, `prod`) y a dos formas de desplegar:

1. **Docker en un servidor fuera de AWS** (on-premise u otra nube).
2. **AWS EKS.**

Se entregan **dos guías separadas**: la de EKS (la actual, `docs/deployment/despliegue-aws-eks.md`, actualizada) y una nueva para Docker (`docs/deployment/despliegue-docker.md`). Cada una con su PDF.

**Las guías también sirven para crear contenido técnico.** Cada fase empieza con tres apartados antes de los comandos:

- **Qué se persigue:** el objetivo de la fase en lenguaje llano.
- **Por qué así:** por qué esta forma es la más segura o la más escalable.
- **Alternativas descartadas:** qué otras formas hay y por qué no se eligieron.

## 2. Decisiones tomadas

| Decisión | Elegido | Descartado y por qué |
|---|---|---|
| Quién lee los secretos | **El propio servicio, al arrancar** (opción A) | Un agente externo (External Secrets en EKS, contenedor de arranque en Docker): dos mecanismos distintos que mantener y 11 piezas más por escenario |
| Cuándo se enteran de un cambio | **Al reiniciar** | Recarga en caliente: obliga a que conexiones de SQL/Redis y llaves ya cargadas sepan recargarse |
| Identidad en Docker fuera de AWS | **IAM Roles Anywhere**, un certificado por servicio | Un usuario de IAM por servicio: claves fijas que valen hasta que alguien las rota |
| Entornos | **`local`, `staging`, `prod`**, cada uno con su propio juego de secretos | — |
| Si falta un secreto | **El servicio no arranca** | Respaldo silencioso a `appsettings.json`: un servicio de prod podría arrancar con valores de desarrollo sin que nadie lo note |
| Borde en EKS | **CloudFront con VPC origin y ALB interno** | ALB público restringido a las IP de CloudFront con cabecera secreta: el ALB seguiría teniendo IP pública |
| Borde en Docker | **Cloudflare Tunnel** (`cloudflared`, sin puertos abiertos) | IP pública con firewall a los rangos de Cloudflare y Authenticated Origin Pulls: rangos que cambian y un certificado más que mantener |

## 3. Qué secretos hay y quién lee cada uno

Los secretos se agrupan **por tema**, no por servicio, para no repetir un mismo valor (la cadena de SQL) en nueve sitios. El nombre completo es `mlmconqueror/{entorno}/{secreto}`.

| Secreto | Contenido (claves del JSON) | AdminAPI | AdminWeb | Billing | BizCenter | BizCenterWeb | CommissionEngine | RankEngine | SharedAPICenter | SignupAPI | Signups | TMS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `datos` | `ConnectionStrings__DefaultConnection`, `ConnectionStrings__HangFire`, `ConnectionStrings__Redis` | ✔ | | ✔ | ✔ | | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| `jwt-firma` | `Jwt__PrivateKeyBase64` | ✔ | | | | | | | | ✔ | | |
| `dp-gateway` | `DataProtection__Certificate__Base64`, `DataProtection__Certificate__Password` | ✔ | | ✔ | ✔ | | | | | | | |
| `signupapi` | `RootAmbassador__Email`, `RootAmbassador__Password`, `Entrax__KeyId`, `Entrax__Secret` | | | | | | | | | ✔ | | |
| `billing` | `Stripe__SecretKey` | | | ✔ | | | | | | | | |
| `sharedapicenter` | `ExternalApi__ApiKey` | | | | | | | | ✔ | | | |
| `portales` | `Syncfusion__LicenseKey` | | ✔ | | | ✔ | | | | | | |
| `firebase` | `Firebase__CredentialsJsonBase64` | | | ✔ | ✔ | | ✔ | ✔ | ✔ | ✔ | | ✔ |
| `twilio` | `Notifications__Sms__Twilio__AccountSid`, `Notifications__Sms__Twilio__AuthToken` | ✔ | | ✔ | ✔ | | | ✔ | | ✔ | | |
| `borde` | `Cloudflare__TunnelToken` | | | | | | | | | | | |

Reglas:

- **`borde` no lo lee ningún servicio**: solo existe en los entornos con Docker y lo baja el script de despliegue para `cloudflared` (sección 5A).
- **Las claves del JSON van en formato de variable de entorno de .NET** (`Seccion__Clave`). Son las mismas que ya enseña la guía de EKS.
- **No van a Secrets Manager** las cosas que no son secretas: la llave pública JWT, URLs, CORS, zona horaria, puertos. Siguen en la configuración normal (ConfigMaps en EKS, `environment` del compose en Docker).
- **S3 y SES** no necesitan secreto: usan la misma identidad de AWS del servicio.
- **Signups** figura con `datos` porque su `appsettings.json` trae cadena de SQL. Si al implementar se comprueba que no la usa, se le quita de la tabla.
- **Certificados retirados del anillo de pasarelas** (`DataProtection:Certificate:Retired:N`): durante una rotación conviven el certificado nuevo y los viejos. Se admiten como `DataProtection__Certificate__Retired__N__Base64` y `__Password` dentro del mismo secreto `dp-gateway`.
- **La tabla vive en un solo archivo del repositorio** (`deploy/aws/secretos-por-servicio.json`). De él salen los permisos de IAM y contra él se prueba la lista de cada servicio (sección 8).

## 4. Cómo lee cada servicio sus secretos

### 4.1 Configuración (no secreta, en el `appsettings.json` de cada servicio)

```json
"SecretosAws": {
  "Habilitado": true,
  "Entorno": "local",
  "Region": "us-east-1",
  "Secretos": [ "datos", "jwt-firma", "signupapi", "firebase", "twilio" ]
}
```

- `Secretos` es la fila del servicio en la tabla de la sección 3.
- `Entorno` vale `local` en el repositorio. En staging y prod se cambia con `SecretosAws__Entorno=staging` / `prod` en el compose o en el ConfigMap.
- Prefijo fijo `mlmconqueror`. No se configura: un prefijo configurable sería otra forma de apuntar a los secretos equivocados.

### 4.2 Al arrancar

`builder.Configuration.AddSecretosDeAws()`, en `MLMConquerorGlobalEdition.SharedKernel.Server`, llamada en los 11 `Program.cs`:

1. Lee `SecretosAws` de la configuración ya cargada.
2. Si `Habilitado` es `false`, no hace nada.
3. Arma los nombres `mlmconqueror/{Entorno}/{secreto}` y los pide a Secrets Manager con el SDK oficial (`AWSSDK.SecretsManager`), usando la cadena de credenciales por defecto del SDK (IRSA en EKS, Roles Anywhere en Docker, el perfil del desarrollador en `local`).
4. Cada clave `Seccion__Clave` del JSON pasa a `Seccion:Clave`.
5. **Prioridad:** la fuente se inserta **justo antes de las variables de entorno**. Queda por encima de `appsettings.json` y `appsettings.{Environment}.json`, y por debajo de las variables de entorno y la línea de comandos, de modo que en una emergencia todavía se puede forzar un valor sin tocar AWS.
6. Escribe en el log los nombres de los secretos cargados y cuántas claves trajo cada uno. **Nunca valores.**

### 4.3 Errores

Con `Habilitado=true`, cualquier fallo detiene el arranque con una excepción cuyo mensaje dice qué secreto y por qué:

| Caso | Mensaje (resumen) |
|---|---|
| `AccessDeniedException` / `AccessDenied` de KMS | La identidad de este servicio no tiene permiso sobre `mlmconqueror/{entorno}/{secreto}` |
| `ResourceNotFoundException` | El secreto no existe en el entorno `{entorno}` |
| Sin credenciales en la cadena del SDK | El contenedor no tiene identidad de AWS (falta IRSA, el certificado de Roles Anywhere o el perfil local) |
| El valor no es un JSON de pares clave-texto | El secreto tiene un formato que no se puede leer |
| `Entorno` vacío o distinto de `local`/`staging`/`prod` | Entorno no válido |

No hay respaldo a `appsettings.json`.

### 4.4 Archivos que pasan a base64

- **PFX del anillo de credenciales de las pasarelas** (el certificado con el que se envuelven las llaves que cifran las credenciales de Spreedly, PayQuicker, Entrax, etc. en la base). Hoy se carga desde `DataProtection:Certificate:Path`. Se agrega `DataProtection:Certificate:Base64`; si está, gana sobre `Path`. `Path` y `Thumbprint` siguen funcionando. Lo mismo para cada `Retired:N`.
- **Firebase**: hoy `Firebase:CredentialsFilePath`. Se agrega `Firebase:CredentialsJsonBase64`; si está, gana sobre el archivo.

### 4.5 En la máquina del desarrollador (`local`)

- Los servicios usan el perfil de AWS del desarrollador (`aws sso login` o el perfil por defecto). Ese perfil lee todo `mlmconqueror/local/*`, porque el desarrollador corre los 11 servicios.
- Para trabajar sin conexión: `SecretosAws:Habilitado=false` en `appsettings.Development.json` (que no se sube al repositorio) y todo funciona como hoy.
- El docker-compose de `deploy/docker` pasa a leer `mlmconqueror/local/*` con el perfil del desarrollador montado de solo lectura. El `.env` se queda solo con lo no secreto (la contraseña `sa` del SQL Server del propio compose sigue ahí, porque ese SQL Server es local y desechable).

## 5. Identidad de cada servicio ante AWS

### 5.1 Común

- **Un rol de IAM por servicio y por entorno:** `MLM-{entorno}-{servicio}` (por ejemplo `MLM-prod-billing`).
- **Permiso:** `secretsmanager:GetSecretValue` solo sobre los ARN de su fila de la tabla, en su entorno; `kms:Decrypt` solo sobre la llave de su entorno.
- **Una llave de KMS por entorno** (`alias/mlmconqueror-{entorno}`) para cifrar los secretos de ese entorno.
- Roles, permisos y llaves los crea `deploy/aws/` a partir de `secretos-por-servicio.json`. Agregar un secreto a un servicio es cambiar ese archivo, la lista del servicio, y volver a correr el script.

### 5.2 EKS

- Cada Deployment tiene su propia cuenta de servicio de Kubernetes, anotada con su rol (IRSA). El SDK la detecta solo.
- **External Secrets Operator sale de la guía**, junto con sus `ExternalSecret` y su rol que leía todo `mlmconqueror/prod/*`.

### 5.3 Docker fuera de AWS: IAM Roles Anywhere

- **Autoridad certificadora propia**, creada con openssl (sin AWS Private CA). Su certificado se registra en Roles Anywhere como *trust anchor*, uno por entorno.
- **Un certificado por servicio y entorno**, con `CN={servicio}` y validez de un año. La política de confianza de cada rol exige ese `CN`: el certificado de Billing solo puede asumir `MLM-{entorno}-billing`.
- **En el contenedor:** la imagen incluye `aws_signing_helper` (binario oficial de AWS). Un `AWS_CONFIG_FILE` con `credential_process` lo invoca con el certificado y la llave montados de solo lectura. El SDK obtiene credenciales temporales de una hora y las renueva solo.
- **Revocación:** se importa la lista de revocación (CRL) en Roles Anywhere, o se desactiva el trust anchor. No hay que tocar los servicios.
- **Renovación:** un script emite los 11 certificados nuevos; se reemplazan los archivos y se reinician los contenedores.
- La llave privada de la autoridad certificadora no vive en el servidor de Docker.

## 5A. El borde: CloudFront en EKS, Cloudflare en Docker

Nadie llega directo a los servicios: en EKS todo entra por CloudFront y en Docker todo entra por Cloudflare.

**Lo que se publica** (igual que la guía actual): portales `admin.`, `app.`, `signup.`; APIs `auth-api.` (SignupAPI), `admin-api.` (AdminAPI), `api.` (BizCenter); rutas parciales de `rank.` (RankEngine) e `integrations.` (SharedAPICenter). Billing, CommissionEngine y TicketManagementSystem nunca se publican; `/swagger`, `/hangfire` y los webhooks sin firma siguen bloqueados.

### EKS: CloudFront con VPC origin

- ALB **interno** (`alb.ingress.kubernetes.io/scheme: internal`), sin IP pública; CloudFront entra por un *VPC origin*.
- CloudFront → ALB por **HTTPS** con el certificado de ACM del dominio, para que `X-Forwarded-Proto` sea `https`.
- **WAF** en CloudFront (alcance `CLOUDFRONT`), no en el ALB.
- Comportamientos con `CachingDisabled` y la política de petición al origen `AllViewer` (reenvía `Host`, cookies, query string y cabeceras; el ALB enruta por nombre). WebSockets habilitados por defecto en CloudFront.
- DNS (Route 53) de cada nombre como alias a la distribución de CloudFront.

### Docker: Cloudflare Tunnel

- Contenedor `cloudflared` en el compose del servidor, con conexión saliente a Cloudflare. **El compose del servidor no publica puertos.**
- Dos redes: `borde` (subred fija, `cloudflared` y los 8 servicios publicados) e `interna` (todos los servicios). Billing, CommissionEngine y TMS solo en `interna`.
- En Cloudflare: un *public hostname* por nombre hacia `http://<servicio>:8080`, y reglas de WAF que bloquean `/swagger`, `/hangfire` y `/api/v1/webhooks/*`.
- El token del túnel vive en `mlmconqueror/{entorno}/borde`. Al desplegar, un script con la credencial del administrador lo escribe en un archivo de solo lectura que se monta en `cloudflared` (`TUNNEL_TOKEN_FILE`). Es el único secreto que queda en disco en el servidor; solo sirve para hacerse pasar por el túnel y se revoca desde Cloudflare.

### La IP real del cliente

- `ForwardLimit` pasa a ser configurable (`ForwardedHeaders:ForwardLimit`, por defecto 1, entre 1 y 3).
- **EKS:** cliente → CloudFront → ALB → pod. CloudFront agrega la IP del cliente a `X-Forwarded-For` y entra al ALB desde IPs de la VPC; el ALB agrega esa IP. Con `ForwardLimit=2` y `KnownNetwork` = CIDR de la VPC, queda la IP que agregó CloudFront. Lo que el cliente escriba antes se ignora: el recorrido se detiene en la primera IP que no es de confianza.
- **Docker:** cliente → Cloudflare → cloudflared → servicio. Cloudflare agrega la IP del cliente al final de `X-Forwarded-For`. Con `ForwardLimit=1` y `KnownNetwork` = subred `borde`, queda esa IP. Un contenedor de la red `interna` no puede falsificar su IP.

## 6. Guías

| Guía | Contenido |
|---|---|
| `docs/deployment/despliegue-aws-eks.md` (actualizada) | Secretos por entorno con la tabla de la sección 3; un rol por servicio con IRSA; sin External Secrets; staging y prod en el mismo procedimiento con `ENTORNO`. PDF regenerado. |
| `docs/deployment/despliegue-docker.md` (nueva) | Servidor fuera de AWS: requisitos, autoridad certificadora, Roles Anywhere, emisión y montaje de certificados, roles, secretos, compose de producción, operación (reinicio tras cambiar un secreto, renovación y revocación de certificados). Con PDF. |
| Sección "Secretos en tu máquina" (en la guía de Docker) | Crear `mlmconqueror/local/*`, entrar con `aws sso login`, trabajar sin conexión. |

## 7. Lo que no se hace

- Recarga en caliente de secretos.
- Rotación automática de secretos con Lambda (se puede agregar después; el diseño no la impide).
- Mover las credenciales de las pasarelas de la base a Secrets Manager: ya están cifradas en la base con el anillo, y el anillo sí pasa a Secrets Manager.
- Cambiar las apps MAUI: no leen secretos del servidor.

## 8. Pruebas

Automáticas, sin AWS (cliente de Secrets Manager simulado detrás de una interfaz):

1. Carga los secretos de la lista y convierte `__` en `:`.
2. Prioridad: gana sobre `appsettings.json` y pierde contra una variable de entorno.
3. El nombre sale del entorno: con `Entorno=local` nunca pide `mlmconqueror/prod/...`.
4. Falla con mensaje claro en: sin permiso, inexistente, sin credencial, JSON inválido, entorno inválido.
5. Con `Habilitado=false` no llama a AWS.
6. El log trae nombres y nunca valores.
7. El PFX (actual y retirados) y Firebase se cargan desde base64, y `Path`/`CredentialsFilePath` siguen funcionando.
8. **Consistencia:** la lista `Secretos` del `appsettings.json` de cada servicio es igual a su fila en `deploy/aws/secretos-por-servicio.json`.

Manual, con la cuenta de Nick: el docker-compose local leyendo `mlmconqueror/local/*` y los 11 servicios sanos.

## 9. Orden de trabajo

1. `AddSecretosDeAws()` y sus pruebas.
2. PFX y Firebase desde base64.
3. `secretos-por-servicio.json`, la lista `Secretos` en los 11 servicios, la llamada en cada `Program.cs` y la prueba de consistencia.
4. `deploy/aws/`: llaves KMS, secretos vacíos por entorno, roles y permisos; Roles Anywhere y emisión de certificados.
5. Docker: `aws_signing_helper` en la imagen, compose de producción con certificados montados por servicio, compose local con el perfil del desarrollador.
6. Guía de EKS actualizada, guía de Docker nueva y sus PDFs.
