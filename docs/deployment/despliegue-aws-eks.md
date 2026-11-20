# Despliegue en AWS EKS — Guía paso a paso

**Fecha:** 20-nov-2026
**Código de referencia:** `master` con la rama `secretos-aws` integrada
**Alcance:** los 11 servicios de MLM Conqueror Global Edition en Amazon EKS, en los entornos `staging` y `prod`, con CloudFront, AWS WAF, AWS Secrets Manager, KMS, IAM (un rol por servicio), RDS for SQL Server, ElastiCache, EFS, S3, SES, ECR, ACM, Route 53, CloudWatch y GitHub Actions.
**Fuera del alcance:** la carga de datos del sistema anterior, la publicación de las apps MAUI en las tiendas (la sección 19 dice qué cambiar en ellas) y el despliegue con Docker en un servidor fuera de AWS, que tiene su propia guía (`docs/deployment/despliegue-docker.md`).

---

## Cómo usar esta guía

- Las fases van en orden y cada una depende de las anteriores. No se salta ninguna.
- Cada fase empieza con tres apartados: **Qué se persigue** (el objetivo en lenguaje llano), **Por qué así** (por qué esta forma es la más segura o la que mejor escala) y **Alternativas descartadas** (qué otras formas hay y por qué no se eligieron). Después vienen los comandos.
- Cada paso dice qué hacer, el comando exacto y cómo comprobar que quedó bien. No se pasa al siguiente paso hasta que la comprobación da lo esperado.
- Todo se ejecuta desde una terminal **Linux** (WSL2 con Ubuntu 24.04 en la máquina Windows). Los comandos son bash; no se ejecutan en PowerShell.
- **El mismo procedimiento sirve para `staging` y para `prod`.** Lo único que cambia es la variable `ENTORNO`, que se fija antes de todo lo demás. Cada entorno va en su propia cuenta de AWS: `staging` en la cuenta de desarrollo de Nick y `prod` en la cuenta de producción. Producción nunca vive en la cuenta de desarrollo.

```
export ENTORNO=prod        # o staging
```

- Los valores que cambian entre instalaciones viven en variables de entorno, en un archivo por entorno (`~/mlm-$ENTORNO.env`) que se crea en la Fase 1. Los manifiestos de Kubernetes usan `${VARIABLE}` y se aplican con la función `aplicar`, que sustituye esas variables antes de mandarlos al clúster.
- Ningún secreto se escribe en el repositorio, en un manifiesto, en un ConfigMap ni en una variable que quede en el historial de la terminal sin borrar. Los secretos van de un archivo temporal a Secrets Manager y el archivo se borra en el mismo paso. Los servicios los leen de Secrets Manager al arrancar; Kubernetes nunca los guarda.
- Esta guía no toca la instancia RDS de producción del sistema anterior. La base del sistema nuevo es una instancia nueva.

## Decisiones que hay que tener antes de empezar

| Decisión | Valor que asume la guía | Dónde se usa |
|---|---|---|
| Entorno | `prod` o `staging`, con `export ENTORNO=...` al principio de cada terminal. Cada uno tiene sus propios secretos (`mlmconqueror/$ENTORNO/*`), su llave KMS (`alias/mlmconqueror-$ENTORNO`) y sus roles (`MLM-$ENTORNO-<servicio>`). | Toda la guía |
| Dominio | `mlmconqueror.com` en `prod` (confirmado por Nick el 19-nov-2026) y `staging.mlmconqueror.com` en `staging`. En DNS no importan las mayúsculas: `MLMConqueror.com` y `mlmconqueror.com` son el mismo dominio. | DNS, certificado, CORS, enlaces de correo |
| Secretos | Cada servicio los lee de AWS Secrets Manager al arrancar, solo los de su lista, con un rol propio (IRSA). Sin External Secrets ni Secrets de Kubernetes. Si un secreto falta, el servicio no arranca. | Fases 6, 9, 10 y 11 |
| Borde | CloudFront delante de un ALB **interno** (sin IP pública), con AWS WAF en CloudFront | Fases 12 y 13 |
| Región de AWS | `us-east-1` | Todo; SES y S3 ya usan esa región en appsettings |
| Zona horaria del negocio | `America/New_York` (Windows: `Eastern Standard Time`), confirmada por Nick el 19-nov-2026. Es la hora del Este con cambio de horario: UTC-4 en verano y UTC-5 en invierno. | `TZ` de los pods y zona de RDS. El código usa la hora del servidor para el negocio. En RDS no se puede cambiar después de crear la instancia. |
| Proyecto de Firebase para push | Sí, con el JSON de la cuenta de servicio | 7 servicios. Sin él, push queda apagado y el resto funciona. |
| Licencia de Syncfusion | Clave de licencia disponible | AdminWeb y BizCenterWeb |
| Entrax (pago en efectivo por QR) | Apagado en el primer despliegue y encendido en la Fase 15 | SignupAPI |
| Cuenta de Stripe | No se usa: el secreto `billing` queda vacío | Billing |
| Twilio (SMS del 2FA) | Apagado hasta tener cuenta | Notificaciones |

Nombres públicos que crea la guía:

| Nombre | Servicio | Para qué es público |
|---|---|---|
| `admin.${DOMINIO}` | AdminWeb | Portal de administración |
| `app.${DOMINIO}` | BizCenterWeb | Portal de miembros |
| `signup.${DOMINIO}` | Signups | Aplicación de alta |
| `auth-api.${DOMINIO}` | SignupAPI | El navegador del alta, las apps MAUI, el JWKS y el callback de Entrax |
| `admin-api.${DOMINIO}` | AdminAPI | App MAUI de administración |
| `api.${DOMINIO}` | BizCenter | App MAUI del centro de negocios |
| `rank.${DOMINIO}` | RankEngine | Solo `/certificates` (los PDF de reconocimiento) |
| `integrations.${DOMINIO}` | SharedAPICenter | Solo `/api/v1/external` |

Billing, CommissionEngine y TicketManagementSystem no tienen nombre público: solo se llega a ellos desde dentro del clúster. `/swagger`, `/hangfire` y los webhooks de SharedAPICenter (`/api/v1/webhooks/*`) tampoco se publican.

---

## 1. Arquitectura

- **Red:** una VPC nueva (`10.20.0.0/16`) en tres zonas de disponibilidad, con subredes públicas (solo para los NAT Gateway) y privadas para el balanceador, los nodos, la base y Redis. Tiene un NAT Gateway por zona.
- **Cómputo:** un clúster EKS con un grupo de nodos administrado de 3 a 6 nodos `m6i.xlarge` en las subredes privadas.
- **Entrada:** el recorrido es cliente → CloudFront → ALB interno → pod.
  - **CloudFront** es la única puerta pública. Termina TLS con un certificado de ACM, filtra con AWS WAF y no guarda nada en caché: todo el contenido es dinámico.
  - CloudFront llega al ALB por un *VPC origin*, una conexión privada dentro de la VPC, y por HTTPS.
  - El **ALB** es **interno**: no tiene IP pública y su grupo de seguridad solo admite las IP de CloudFront. Lo crea el AWS Load Balancer Controller, enruta por nombre de host y da afinidad de sesión (stickiness) a los portales Blazor.
- **Dentro del clúster:**
  - Cada servicio escucha HTTP en el puerto 8080.
  - Los servicios se llaman entre sí por HTTP interno: `http://<servicio>.mlmconqueror.svc.cluster.local`.
- **Datos:**
  - RDS for SQL Server Standard en Multi-AZ, con una sola base `MLMConquerorGlobalEditionDb`. La comparten los 8 servicios con base y Hangfire.
  - ElastiCache Redis con TLS y token.
  - S3 para archivos.
  - EFS para lo que los servicios escriben en disco: adjuntos, recibos de pago, certificados PDF y llaves de Data Protection.
- **Secretos:**
  - Viven en AWS Secrets Manager (`mlmconqueror/$ENTORNO/<secreto>`), cifrados con la llave KMS del entorno.
  - Cada servicio, al arrancar, lee **solo los secretos de su lista** y los mezcla en su configuración. Kubernetes no guarda ninguno: no hay Secrets de Kubernetes ni archivos de secretos montados.
  - Para cambiar un secreto se carga el valor nuevo y se reinician los servicios que lo leen.
- **Permisos de AWS de los pods:** IRSA (IAM Roles for Service Accounts). Cada Deployment tiene su propia cuenta de servicio, asociada a su propio rol (`MLM-$ENTORNO-<servicio>`), que solo puede leer los secretos de su fila y, si los usa, S3 y SES. No hay llaves de acceso de AWS en la configuración.
- **Imágenes:** Amazon ECR, un repositorio por servicio.
- **Observabilidad:** CloudWatch Container Insights recoge métricas y logs, y las alarmas avisan por SNS.

| Servicio | Proyecto | Base de datos | Redis | Hangfire (cola) | Disco en EFS | Secretos que lee | S3 y SES |
|---|---|---|---|---|---|---|---|
| signupapi | SignupAPI | Sí | Sí | `signups` | Llaves DP | `datos`, `jwt-firma`, `signupapi`, `firebase`, `twilio` | Sí |
| adminapi | AdminAPI | Sí | Sí | No | Adjuntos, recibos | `datos`, `jwt-firma`, `dp-gateway`, `twilio` | Sí |
| billing | Billing | Sí | Sí | 8 colas `billing*` | Recibos | `datos`, `dp-gateway`, `billing`, `firebase`, `twilio` | Sí |
| bizcenter | BizCenter | Sí | Sí | `bizcenter` | Adjuntos | `datos`, `dp-gateway`, `firebase`, `twilio` | Sí |
| commissionengine | CommissionEngine | Sí | Sí | `commissions` | No | `datos`, `firebase` | No |
| rankengine | RankEngine | Sí | Sí | `rank` | Certificados | `datos`, `firebase`, `twilio` | Sí |
| ticketmanagementsystem | TicketManagementSystem | Sí | Sí | `tickets` | No | `datos`, `firebase` | No |
| sharedapicenter | SharedAPICenter | Sí | Sí | No | No | `datos`, `sharedapicenter`, `firebase` | No |
| adminweb | AdminWeb | No | No | No | Llaves DP | `portales` | No |
| bizcenterweb | BizCenterWeb | No | No | No | Llaves DP | `portales` | No |
| signups | Signups | No | No | No | Llaves DP | Ninguno | No |

La columna de secretos sale de `deploy/aws/secretos-por-servicio.json`, la única tabla de quién lee qué (sección 8.1).

Las migraciones de la base se aplican **una vez, antes de arrancar los servicios**, con el script idempotente `DeploymentScripts/migrate-rds.sql`. Los servicios también llaman a `MigrateAsync()` al arrancar; con la base ya migrada no hacen nada.

---

## 2. Fase 0 — Cambios de código obligatorios antes de construir las imágenes

**Qué se persigue:** que el código esté listo para correr en un clúster antes de construir una sola imagen. El código de `17197f4` estaba hecho para correr directamente en Kestrel en una máquina, con los secretos en `appsettings.json` o en variables escritas a mano. En EKS corre detrás de CloudFront y de un balanceador, con varias réplicas, en Linux, y con los secretos en AWS Secrets Manager.

**Por qué así:** cada cambio cierra un fallo o un agujero concreto que solo aparece en producción: la IP del cliente que se podía falsear, las sesiones que se perdían en cada reinicio, un panel abierto, contraseñas en el repositorio, secretos al alcance de cualquiera que pudiera leer la configuración. Arreglarlo en el código, y no con parches en la infraestructura, hace que la misma imagen se comporte igual en Docker local, en `staging` y en `prod`, y que las pruebas automáticas lo vigilen en cada cambio.

**Alternativas descartadas:**
- **Resolverlo solo en la infraestructura** (reescribir cabeceras en el balanceador, montar archivos de configuración distintos por entorno): cada entorno tendría su propia versión de la verdad y nadie la probaría antes de desplegar.
- **Una imagen por entorno:** obliga a reconstruir para pasar de `staging` a `prod`, y lo que se probó deja de ser lo que se despliega.

Esta sección dice qué se cambió y dónde, para que se pueda revisar. Los cambios 1 a 7 están en la rama `fase0-eks`; los de secretos (8 a 10) y el número de saltos del cambio 2, en la rama `secretos-aws`. Las imágenes se construyen solo desde un `master` que ya los tenga integrados (sección 2.7).

| # | Problema | Cambio | Dónde |
|---|---|---|---|
| 1 | No había cómo construir imágenes | `Dockerfile` único para los 11 servicios y `.dockerignore` | Raíz del repositorio |
| 2 | Detrás del ALB los servicios veían la IP del balanceador y `http`; el límite por IP se podía falsear con `X-Real-IP` | `AddProxyForwardedHeaders` y `app.UseForwardedHeaders()` como primer middleware en los 11 `Program.cs`. El número de saltos de confianza es configurable (`ForwardedHeaders:ForwardLimit`, 1 por defecto, entre 1 y 3); en EKS vale 2 porque CloudFront va delante del ALB | `SharedKernel.Server/Hosting/ProxyHosting.cs` |
| 2b | SignupAPI tomaba la primera IP de `X-Forwarded-For`, que escribe el cliente | `ResolveClientIp()` usa `Connection.RemoteIpAddress` | `SignupAPI/Controllers/SignupsController.cs` |
| 3 | Cuatro servicios perdían sus llaves de Data Protection en cada reinicio: sesiones, antifalsificación, enlaces de correo y PII de SignupAPI | `AddPersistentDataProtectionKeys` con `DataProtection:KeysPath` | SignupAPI, AdminWeb, BizCenterWeb y Signups |
| 4 | El alta tenía `https://localhost:7005` fijo en el navegador | El servidor de Signups sirve el `appsettings.json` del cliente con `SignupApiPublicUrl`, así que la misma imagen vale en Docker local y en EKS | `Signups/Middleware/ClientConfigurationMiddleware.cs` y `Signups.Client/Program.cs` |
| 5 | La licencia de Syncfusion no se registraba | `RegisterLicense(Syncfusion:LicenseKey)` y referencia a `Syncfusion.Licensing` | AdminWeb y BizCenterWeb |
| 6 | El panel de Hangfire de TicketManagementSystem estaba abierto (`Authorization = []`) | Filtro por defecto: solo peticiones locales | `TicketManagementSystem/Program.cs` |
| 7 | Seis `appsettings.json` tenían comentada una cadena de conexión con contraseña a la copia restaurada de la base de producción anterior, y Signups una contraseña de `RootAmbassador` | Líneas borradas | Billing, CommissionEngine, RankEngine, SharedAPICenter, TicketManagementSystem y Signups |
| 8 | Los secretos llegaban por variables de entorno que alguien tenía que escribir, y quien pudiera leer la configuración del clúster podía leerlos | Cada servicio lee de Secrets Manager, al arrancar, solo los secretos de su lista (`SecretosAws:Secretos` en su `appsettings.json`), con `builder.Configuration.AddSecretosDeAws()` justo después de crear el builder | `SharedKernel.Server/Hosting/SecretosAwsConfiguracion.cs` y los 11 `Program.cs` |
| 9 | El PFX del anillo de las pasarelas y la credencial de Firebase solo se podían dar como archivo | `DataProtection:Certificate:Base64` (también para cada `Retired:N`) y `Firebase:CredentialsJsonBase64`, que ganan sobre `Path` y `CredentialsFilePath`. Así llegan desde Secrets Manager sin montar archivos | `Repository/Services/Payout/GatewayCredentialProtectionExtensions.cs` y `Repository/Services/FirebaseCredentialLoader.cs` |
| 10 | Los `appsettings.json` traían valores de relleno con forma de secreto (`Stripe:SecretKey`, `ExternalApi:ApiKey`, `RootAmbassador:Password`) y Signups unas cadenas de conexión que no usaba | Valores vacíos; los reales llegan de Secrets Manager. Signups no lee ningún secreto | `appsettings.json` de Billing, SharedAPICenter, SignupAPI y Signups |

Hay pruebas automáticas que cubren estos cambios:
- Desde la red de la VPC se toma la IP que añadió el primer proxy de confianza y no la que escribió el cliente, con uno y con dos saltos (CloudFront delante del ALB). Desde fuera de la VPC la cabecera no cambia nada.
- Lo que protege un pod lo lee otro con la misma carpeta de llaves.
- Los secretos de la lista se cargan, ganan sobre `appsettings.json` y pierden contra una variable de entorno. El nombre sale del entorno: con `local` nunca se pide un secreto de `prod`.
- Si un secreto no existe, si falta permiso, si el pod no tiene identidad de AWS o si el valor no es un JSON de pares clave-texto, el servicio no arranca y el mensaje dice qué secreto y por qué. El log trae nombres de secretos y cantidad de claves, nunca valores.
- El PFX (actual y retirados) y Firebase se cargan desde base64, y el archivo sigue funcionando.
- La lista de secretos del `appsettings.json` de cada servicio es igual a su fila en `deploy/aws/secretos-por-servicio.json`.

### 2.1 Cómo se comporta cada cambio en producción

- **Forwarded headers:**
  - Con `ForwardedHeaders__KnownNetwork` igual al CIDR de la VPC y `ForwardedHeaders__ForwardLimit` en 2 (Fase 10), se confía en el salto del ALB y en el de CloudFront, que entra al ALB desde IP de la VPC. Queda la IP que agregó CloudFront, que es la del cliente.
  - Lo que el cliente escriba antes en `X-Forwarded-For` se ignora: el recorrido se detiene en la primera IP que no es de confianza.
  - Sin `KnownNetwork` (desarrollo) solo se confía en la propia máquina.
  - El límite de peticiones por IP usa la IP resultante, porque en producción `IpRateLimiting__RealIpHeader` va vacío (Fase 10).
- **Secretos:**
  - `SecretosAws__Entorno` (Fase 10) elige el entorno: el servicio pide `mlmconqueror/$ENTORNO/<secreto>` para cada nombre de su lista.
  - Cada clave del JSON (`Seccion__Clave`) pasa a `Seccion:Clave`. Los secretos ganan sobre `appsettings.json` y pierden contra una variable de entorno, de modo que en una emergencia se puede forzar un valor sin tocar AWS.
  - El log de arranque trae una línea por secreto, sin valores: `[SecretosAws] mlmconqueror/prod/datos: 3 clave(s).`
  - Si un secreto falla, el servicio no arranca. No hay respaldo a `appsettings.json`: un servicio de `prod` nunca arranca con valores de desarrollo sin que nadie lo note.
  - Se leen una sola vez, al arrancar. Para que un servicio vea un valor nuevo hay que reiniciarlo (sección 16.4).
- **Llaves de Data Protection:**
  - Con `DataProtection__KeysPath=/keys` se guardan en el volumen EFS de cada servicio.
  - Los nombres de aplicación (`MLMConqueror.SignupAPI`, `MLMConqueror.AdminWeb`, `MLMConqueror.BizCenterWeb`, `MLMConqueror.Signups`) no se cambian nunca: cambiarlos invalida todo lo protegido.
  - No se usa la tabla `DataProtectionKeys` de la base, que es del anillo de credenciales de las pasarelas.
- **Alta en el navegador:** la dirección pública de SignupAPI es la variable `SignupApiPublicUrl` del servicio `signups` (Fase 10). Si cambia el dominio, se cambia la variable; no hace falta reconstruir la imagen.
- **Syncfusion:** sin `Syncfusion__LicenseKey` los portales arrancan igual, pero muestran el aviso de licencia.

### 2.2 Credenciales que siguen en el historial de git

Borrar las líneas no las saca del historial. La contraseña de SQL de la copia restaurada de la base anterior (`mwrlivemssql-newproddb-restored`) **hay que cambiarla en esa instancia**, coordinándolo con quien la administra. La contraseña de `RootAmbassador` de producción será otra, generada en la Fase 6.

### 2.3 Prueba de una imagen en local

Desde WSL, en la raíz del repositorio:

```
docker build --build-arg PROJECT=MLMConquerorGlobalEdition.Signups -t prueba-signups .
docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development prueba-signups
# en otra terminal:
curl -s http://localhost:8080/health
```

La imagen de compilación es `sdk:10.0.203`, de la misma banda que `global.json` (10.0.2xx); con la etiqueta genérica `sdk:10.0` no arranca. La restauración respeta los `packages.lock.json` sin modo bloqueado, porque los proyectos WebAssembly guardan en el lockfile paquetes atados al parche exacto del SDK.

### 2.5 ImageSharp eliminado de AdminAPI

AdminAPI usaba `SixLabors.ImageSharp` 4.1.3 en la subida de imágenes de producto. Esa versión exige una licencia comercial para compilar en Release. Solo servía para dos cosas:
- Comprobar que el archivo era una imagen.
- Generar una miniatura que ninguna pantalla usaba.

Se quitó la biblioteca. Ahora la subida comprueba la firma de los primeros bytes de cada formato (PNG, JPEG, GIF, WebP) y exige que coincida con la extensión. La miniatura ya no se genera, y la respuesta trae solo `Url`.

### 2.6 Prueba completa en una sola máquina (docker-compose)

`deploy/docker/docker-compose.yml` levanta los 11 servicios con las mismas imágenes que van a EKS, junto con SQL Server 2022 y Redis. La configuración es la de producción, sin TLS. Sirve para probar el despliegue antes de tocar EKS.

Los secretos salen de Secrets Manager, entorno `local` (`mlmconqueror/local/*`), con el perfil de AWS de quien lo levanta, montado de solo lectura en cada contenedor. Cómo crear esos secretos y entrar con `aws sso login` está en `docs/deployment/despliegue-docker.md`, sección 12, "Tu máquina (entorno `local`)".

```
bash deploy/docker/generar-secretos.sh   # una vez: .env con la contraseña del SQL Server de prueba
docker compose -f deploy/docker/docker-compose.yml up -d --build
docker compose -f deploy/docker/docker-compose.yml ps    # todos healthy
```

| Servicio | Dirección |
|---|---|
| Admin | `http://localhost:8101` |
| Centro de negocios | `http://localhost:8104` |
| Alta | `http://localhost:8147` |
| SignupAPI | `http://localhost:8105` |
| SQL Server | `localhost,14333` (usuario `sa`, contraseña `SA_PASSWORD` de `deploy/docker/.env`) |

- SignupAPI arranca primero, aplica las migraciones sobre la base vacía y siembra la cuenta raíz con el correo y la contraseña del secreto `mlmconqueror/local/signupapi`. Los demás servicios esperan a que esté sano.
- Cada contenedor escribe en su log las líneas `[SecretosAws]` con los secretos que cargó. Si falta el perfil de AWS, el contenedor se detiene con el mensaje "no tiene identidad de AWS".
- Los puertos 8101 a 8147 no chocan con los de desarrollo (5001–5011, 7001–7011 y 7147) ni con un SQL Server local en el 1433.
- `deploy/docker/.env` no se sube al repositorio.

### 2.7 Cierre de la Fase 0

En Windows, con la rama `fase0-eks` integrada en `master`:

```
dotnet build MLMConquerorGlobalEdition.slnx -c Release
dotnet test MLMConquerorGlobalEdition.slnx -c Release
git log --oneline -1   # anotar el commit: es el que se despliega
```

Las pruebas tienen que estar en verde. Se anota el commit de `master` que resulte, porque es el que se construye en la Fase 7.

---

## 3. Fase 1 — Cuenta de AWS y herramientas

**Qué se persigue:** tener una cuenta de AWS protegida, una persona que trabaja en ella con credenciales que caducan, y una terminal con todas las herramientas y todas las variables del entorno en un solo archivo.

**Por qué así:**
- El usuario raíz de la cuenta lo puede todo, incluso cerrarla. Con MFA y guardado en un cajón, un robo de contraseña no basta para tomar la cuenta.
- IAM Identity Center da credenciales de unas horas: si se filtran de la terminal, caducan solas. Una llave de acceso de un usuario de IAM vale hasta que alguien la rota.
- CloudTrail deja constancia de quién hizo qué, también de quién leyó un secreto.
- Un archivo de variables por entorno (`~/mlm-$ENTORNO.env`) evita escribir dos veces el mismo valor y que `staging` y `prod` se mezclen en la misma terminal.

**Alternativas descartadas:**
- **Trabajar con el usuario raíz o con un usuario de IAM con llaves de acceso:** credenciales que no caducan, sin registro de quién es la persona detrás.
- **`staging` y `prod` en la misma cuenta:** un error de permisos en `staging` alcanzaría a producción, y nadie podría decir con seguridad quién puede leer qué. Cada entorno va en su cuenta.

### 3.1 Cuenta y acceso

1. Entrar con el usuario raíz y activar MFA en él. No se vuelve a usar para el trabajo diario.
2. En IAM Identity Center, crear un usuario administrador con MFA y el permiso `AdministratorAccess`. Todo lo que sigue se hace con ese usuario.
3. En Billing, crear un presupuesto (AWS Budgets) mensual con aviso al 80% y al 100%.
4. En la región `us-east-1`, activar CloudTrail con un trail para toda la cuenta (Management events, en un bucket propio).

En `staging` la cuenta es la de desarrollo de Nick, que ya tiene estos cuatro puntos. Se comprueba que estén y se sigue.

### 3.2 Herramientas en WSL (Ubuntu 24.04)

```
sudo apt-get update
sudo apt-get install -y unzip jq gettext-base openssl git curl ca-certificates dnsutils
# AWS CLI v2
curl -sSL https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip -o awscliv2.zip
unzip -q awscliv2.zip && sudo ./aws/install && rm -rf aws awscliv2.zip
# kubectl
K=$(curl -sL https://dl.k8s.io/release/stable.txt)
curl -sSLO "https://dl.k8s.io/release/$K/bin/linux/amd64/kubectl"
sudo install -m 0755 kubectl /usr/local/bin/kubectl && rm kubectl
# eksctl
curl -sSL "https://github.com/eksctl-io/eksctl/releases/latest/download/eksctl_Linux_amd64.tar.gz" \
  | tar xz -C /tmp && sudo mv /tmp/eksctl /usr/local/bin/
# helm
curl -fsSL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
# .NET 10 SDK (para generar el script de migraciones)
sudo apt-get install -y dotnet-sdk-10.0
dotnet tool install --global dotnet-ef
```

Docker se instala con Docker Desktop para Windows, activando la integración con la distribución de WSL (Settings, Resources, WSL integration).

**Comprobar:**

```
aws --version; kubectl version --client; eksctl version; helm version
docker version; dotnet --version; envsubst --version | head -1; jq --version
```

### 3.3 Iniciar sesión en AWS desde la terminal

```
export ENTORNO=prod        # o staging
aws configure sso    # perfil: mlm-prod o mlm-staging, region: us-east-1, salida: json
export AWS_PROFILE=mlm-$ENTORNO
aws sso login
aws sts get-caller-identity
```

La cuenta que muestra tiene que ser la del entorno: la de producción con `prod` y la de desarrollo con `staging`.

### 3.4 Archivo de variables de la guía

Se crea una sola vez por entorno. En cada terminal nueva se carga con:

```
export ENTORNO=prod        # o staging
source ~/mlm-$ENTORNO.env
```

Para crearlo:

```
case $ENTORNO in prod) D=mlmconqueror.com;; staging) D=staging.mlmconqueror.com;; esac
cat > ~/mlm-$ENTORNO.env <<EOF
export ENTORNO=$ENTORNO
export AWS_PROFILE=mlm-$ENTORNO
export AWS_REGION=us-east-1
export CUENTA=\$(aws sts get-caller-identity --query Account --output text)
export CLUSTER=mlmconqueror-$ENTORNO
export NS=mlmconqueror
export DOMINIO=$D
export VPC_CIDR=10.20.0.0/16
export ECR=\$CUENTA.dkr.ecr.\$AWS_REGION.amazonaws.com
export BUCKET=mlmconqueror-$ENTORNO-archivos-\$CUENTA
export SES_FROM=no-reply@\$DOMINIO
export TZ_NEGOCIO=America/New_York
export REPO=~/MLMConquerorGlobalMicroServices
EOF
source ~/mlm-$ENTORNO.env
echo $ENTORNO $CUENTA $CLUSTER $DOMINIO $BUCKET
```

A lo largo de la guía se van agregando más variables con `echo "export X=..." >> ~/mlm-$ENTORNO.env`.

Esta es la función que aplica manifiestos. Sustituye solo las variables de la guía y deja intacto cualquier otro `$` del YAML:

```
cat >> ~/mlm-$ENTORNO.env <<'EOF'
VARS_GUIA='$ENTORNO $AWS_REGION $CUENTA $CLUSTER $NS $DOMINIO $VPC_CIDR $ECR $BUCKET $SES_FROM
 $TZ_NEGOCIO $TAG $CERT_ARN $EFS_ID $RDS_HOST $SG_ALB $JWT_PUBLICA'
aplicar() { envsubst "$VARS_GUIA" < "$1" | kubectl apply -f -; }
EOF
source ~/mlm-$ENTORNO.env
```

### 3.5 Copia limpia del repositorio

```
git clone https://github.com/NickSagaracorp/MLMConquerorGlobalMicroServices.git $REPO
cd $REPO && git checkout master && git log --oneline -1
mkdir -p ~/mlm-k8s/$ENTORNO     # aqui se guardan los manifiestos de las fases 5 a 12
```

El commit que muestra tiene que ser el anotado al cerrar la Fase 0.

---

## 4. Fase 2 — DNS y certificado

**Qué se persigue:** que AWS controle los nombres del dominio del entorno y tenga un certificado TLS válido para todos ellos, antes de crear nada que los use.

**Por qué así:**
- Con la zona en Route 53, los nombres públicos pueden ser *alias* de CloudFront (Fase 13) y el certificado se valida solo por DNS.
- El certificado de ACM se renueva solo y su llave privada nunca sale de AWS: nadie la tiene en un disco.
- Un solo certificado con el dominio y el comodín (`*.$DOMINIO`) cubre los ocho nombres públicos. Se pide en `us-east-1`, que es la región que exige CloudFront, y sirve también para el ALB, que está en la misma región.

**Alternativas descartadas:**
- **Comprar un certificado y subirlo:** hay que renovarlo a mano cada año y guardar la llave privada en algún sitio.
- **Un certificado por nombre:** ocho renovaciones y ocho validaciones para lo mismo.
- **Dejar el DNS en el proveedor actual con registros CNAME:** el dominio raíz no admite CNAME y la validación del certificado depende de otra consola.

### 4.1 Zona de Route 53

Si el dominio aún no tiene zona en Route 53:

```
aws route53 create-hosted-zone --name $DOMINIO --caller-reference "mlm-$(date +%s)"
```

Si el dominio está registrado en otro proveedor, se copian los 4 servidores `NS` que devuelve el comando a los servidores de nombre del dominio en ese proveedor.

En `staging` la zona es `staging.mlmconqueror.com`, en la cuenta de desarrollo. Se delega desde la zona de `mlmconqueror.com` con un registro `NS` llamado `staging.mlmconqueror.com` que lleva los 4 servidores que devolvió el comando.

```
export ZONA_ID=$(aws route53 list-hosted-zones-by-name --dns-name $DOMINIO \
  --query 'HostedZones[0].Id' --output text | sed 's#/hostedzone/##')
echo "export ZONA_ID=$ZONA_ID" >> ~/mlm-$ENTORNO.env
dig +short NS $DOMINIO      # tiene que devolver los NS de Route 53
```

### 4.2 Certificado de ACM (dominio y comodín)

Se pide en `us-east-1`. Si algún día `AWS_REGION` es otra, este certificado sigue en `us-east-1` para CloudFront y se pide otro igual en la región del clúster para el ALB.

```
export CERT_ARN=$(aws acm request-certificate --region us-east-1 --domain-name $DOMINIO \
  --subject-alternative-names "*.$DOMINIO" --validation-method DNS \
  --query CertificateArn --output text)
echo "export CERT_ARN=$CERT_ARN" >> ~/mlm-$ENTORNO.env
aws acm describe-certificate --certificate-arn $CERT_ARN \
  --query 'Certificate.DomainValidationOptions[0].ResourceRecord'
```

Con el `Name` y el `Value` que devuelve se crea el registro de validación:

```
cat > /tmp/val.json <<EOF
{"Changes":[{"Action":"UPSERT","ResourceRecordSet":{"Name":"<Name>","Type":"CNAME",
 "TTL":300,"ResourceRecords":[{"Value":"<Value>"}]}}]}
EOF
aws route53 change-resource-record-sets --hosted-zone-id $ZONA_ID --change-batch file:///tmp/val.json
aws acm wait certificate-validated --certificate-arn $CERT_ARN
```

**Comprobar:** `aws acm describe-certificate --certificate-arn $CERT_ARN --query Certificate.Status` devuelve `"ISSUED"`.

---

## 5. Fase 3 — Clúster EKS y red

**Qué se persigue:** una red propia y un clúster de Kubernetes administrado por AWS donde corren los 11 servicios, repartidos en tres zonas de disponibilidad, sin que ningún nodo tenga IP pública.

**Por qué así:**
- **EKS** deja a AWS el plano de control (el cerebro del clúster): sus parches, su copia y su alta disponibilidad. El equipo solo opera los servicios.
- **Tres zonas:** si un centro de datos de AWS cae, los servicios siguen en los otros dos.
- **Nodos en subredes privadas:** nadie puede conectarse a un nodo desde internet. Salen a internet (para ECR, Secrets Manager y las pasarelas de pago) por un NAT Gateway en cada zona.
- **OIDC activado (`withOIDC`):** es lo que permite que cada pod tenga su propio rol de AWS (IRSA, Fase 9) sin llaves de acceso.
- **Discos de los nodos cifrados** y registros de auditoría del clúster en CloudWatch durante 90 días.

**Alternativas descartadas:**
- **ECS o máquinas virtuales con Docker:** sirven, pero EKS ya era la decisión de la plataforma y da réplicas, despliegue gradual y reparto por zonas sin escribirlo a mano.
- **Fargate:** no admite el aprovisionamiento dinámico de volúmenes EFS que usa la Fase 5, y cuesta más con servicios que corren todo el día.
- **Un solo NAT Gateway:** más barato, pero si cae su zona los servicios de las otras dos se quedan sin salida a internet.

### 5.1 Versión de Kubernetes

```
aws eks describe-cluster-versions --default-only \
  --query 'clusterVersions[0].clusterVersion' --output text
```

La versión que devuelve es la que se usa en `K8S_VERSION`. Se elige la versión por defecto de EKS, no la más nueva.

```
echo "export K8S_VERSION=<version>" >> ~/mlm-$ENTORNO.env && source ~/mlm-$ENTORNO.env
```

### 5.2 Definición del clúster

```
cat > ~/mlm-k8s/$ENTORNO/cluster.yaml <<EOF
apiVersion: eksctl.io/v1alpha5
kind: ClusterConfig
metadata:
  name: $CLUSTER
  region: $AWS_REGION
  version: "$K8S_VERSION"
vpc:
  cidr: $VPC_CIDR
  nat:
    gateway: HighlyAvailable
  clusterEndpoints:
    publicAccess: true
    privateAccess: true
availabilityZones: ["${AWS_REGION}a", "${AWS_REGION}b", "${AWS_REGION}c"]
accessConfig:
  authenticationMode: API_AND_CONFIG_MAP
iam:
  withOIDC: true
addons:
  - name: vpc-cni
  - name: coredns
  - name: kube-proxy
  - name: eks-pod-identity-agent
  - name: aws-efs-csi-driver
    wellKnownPolicies:
      efsCSIController: true
  - name: metrics-server
managedNodeGroups:
  - name: general
    instanceType: m6i.xlarge
    minSize: 3
    desiredCapacity: 3
    maxSize: 6
    volumeSize: 50
    volumeEncrypted: true
    privateNetworking: true
    iam:
      withAddonPolicies:
        cloudWatch: true
cloudWatch:
  clusterLogging:
    enableTypes: ["api", "audit", "authenticator"]
    logRetentionInDays: 90
EOF
```

### 5.3 Crear el clúster

Tarda de 20 a 30 minutos:

```
eksctl create cluster -f ~/mlm-k8s/$ENTORNO/cluster.yaml
```

**Comprobar:**

```
kubectl get nodes                      # 3 nodos Ready
kubectl get pods -n kube-system        # todo Running
```

### 5.4 Datos de la red para las fases siguientes

```
export VPC_ID=$(aws eks describe-cluster --name $CLUSTER \
  --query cluster.resourcesVpcConfig.vpcId --output text)
export CLUSTER_SG=$(aws eks describe-cluster --name $CLUSTER \
  --query cluster.resourcesVpcConfig.clusterSecurityGroupId --output text)
export SUBNETS_PRIVADAS=$(aws ec2 describe-subnets \
  --filters Name=vpc-id,Values=$VPC_ID Name=tag:kubernetes.io/role/internal-elb,Values=1 \
  --query 'Subnets[].SubnetId' --output text)
cat >> ~/mlm-$ENTORNO.env <<EOF
export VPC_ID=$VPC_ID
export CLUSTER_SG=$CLUSTER_SG
export SUBNETS_PRIVADAS="$SUBNETS_PRIVADAS"
EOF
echo $VPC_ID $CLUSTER_SG $SUBNETS_PRIVADAS     # tres subredes
```

### 5.5 Namespace

```
kubectl create namespace $NS
kubectl label namespace $NS app.kubernetes.io/part-of=mlmconqueror
```

---

## 6. Fase 4 — Componentes del clúster

**Qué se persigue:** instalar en el clúster las dos piezas que no trae de serie: el controlador que crea el balanceador a partir de los Ingress de Kubernetes y la recogida de métricas y logs.

**Por qué así:**
- El **AWS Load Balancer Controller** traduce los Ingress (Fase 12) a un ALB con sus reglas, certificados y destinos, y lo mantiene al día cuando cambian los pods. Manda el tráfico directo a la IP de cada pod, sin pasar por puertos abiertos en los nodos.
- **Container Insights** es el complemento oficial de EKS: logs y métricas en CloudWatch sin otro sistema que operar.
- El controlador tiene su propio rol de IAM (IRSA) con solo los permisos que publica su proyecto.
- **No se instala nada para los secretos.** Cada servicio los lee de Secrets Manager con su propio rol (Fases 6 y 9). Hay una pieza menos que actualizar, y ningún componente del clúster puede leer todos los secretos.

**Alternativas descartadas:**
- **Un controlador de Ingress propio (NGINX) detrás de un balanceador de red:** otro componente que parchear y escalar, y el TLS y el enrutado quedarían dentro del clúster en lugar de en servicios administrados.
- **External Secrets Operator** para copiar los secretos a Secrets de Kubernetes: necesita un rol que lee todos los secretos del entorno, deja una copia de cada uno en el clúster (legible por quien tenga permiso sobre los Secrets del namespace) y es una pieza más que actualizar. Con la lectura directa, cada servicio solo alcanza lo suyo.
- **Prometheus y Grafana dentro del clúster:** más completos, pero hay que operarlos y guardar sus datos. Para el arranque basta CloudWatch.

### 6.1 AWS Load Balancer Controller

```
LBC_REPO=kubernetes-sigs/aws-load-balancer-controller
LBC_VERSION=$(curl -s https://api.github.com/repos/$LBC_REPO/releases/latest | jq -r .tag_name)
curl -sSL -o /tmp/lbc-policy.json \
  "https://raw.githubusercontent.com/$LBC_REPO/$LBC_VERSION/docs/install/iam_policy.json"
aws iam create-policy --policy-name MLM-AWSLoadBalancerController \
  --policy-document file:///tmp/lbc-policy.json
eksctl create iamserviceaccount --cluster $CLUSTER --namespace kube-system \
  --name aws-load-balancer-controller --role-name MLM-AWSLoadBalancerController \
  --attach-policy-arn arn:aws:iam::$CUENTA:policy/MLM-AWSLoadBalancerController --approve
helm repo add eks https://aws.github.io/eks-charts && helm repo update
helm install aws-load-balancer-controller eks/aws-load-balancer-controller -n kube-system \
  --set clusterName=$CLUSTER --set serviceAccount.create=false \
  --set serviceAccount.name=aws-load-balancer-controller \
  --set region=$AWS_REGION --set vpcId=$VPC_ID
```

**Comprobar:** `kubectl get deployment -n kube-system aws-load-balancer-controller` muestra `2/2`.

### 6.2 CloudWatch Container Insights (métricas y logs)

```
aws eks create-addon --cluster-name $CLUSTER --addon-name amazon-cloudwatch-observability
aws eks wait addon-active --cluster-name $CLUSTER --addon-name amazon-cloudwatch-observability
```

Los logs de los pods llegan al grupo `/aws/containerinsights/$CLUSTER/application`. Se les pone una retención de 90 días:

```
aws logs put-retention-policy --log-group-name /aws/containerinsights/$CLUSTER/application \
  --retention-in-days 90
```

Si el grupo todavía no existe, este comando se repite al terminar la Fase 11.

---

## 7. Fase 5 — Datos: RDS, ElastiCache, EFS, S3 y SES

**Qué se persigue:** la base de datos, la caché, el disco compartido, el almacén de archivos y el correo, todos administrados por AWS, cifrados con la llave del entorno y alcanzables solo desde el clúster.

**Por qué así:**
- **Servicios administrados:** AWS hace las copias, los parches y la conmutación a otra zona si una cae (RDS Multi-AZ, Redis con réplica). Nadie tiene que operar un SQL Server.
- **Grupos de seguridad que solo admiten a los nodos del clúster:** la base, Redis y EFS no son alcanzables ni desde internet ni desde otro recurso de la VPC.
- **Una llave KMS por entorno** (`alias/mlmconqueror-$ENTORNO`) cifra los secretos, la base, Redis, EFS y S3. Quien no tenga permiso sobre la llave no puede leer nada aunque consiga una copia. Rota cada año sola.
- **La contraseña maestra de RDS la genera RDS** y la guarda en Secrets Manager: nadie la escribe ni la ve.
- **SES** envía con la identidad de AWS de cada servicio; no hay contraseña SMTP que guardar.

**Alternativas descartadas:**
- **SQL Server y Redis en contenedores dentro del clúster:** el equipo tendría que hacer copias, alta disponibilidad y parches de dos bases de datos.
- **Llaves administradas por AWS (`aws/rds`, `aws/s3`...)** en lugar de una llave propia: no se puede limitar quién las usa ni compartirlas entre servicios, y no dejan claro qué entorno cifra cada una.
- **Correo por SMTP con usuario y contraseña:** otro secreto más que se puede filtrar y que hay que rotar.

### 7.1 Grupos de seguridad

Solo los nodos del clúster pueden llegar a SQL Server (1433), a Redis (6379) y a EFS (2049):

```
for n in rds redis efs; do
  aws ec2 create-security-group --group-name mlm-$ENTORNO-$n --description "mlm $ENTORNO $n" \
    --vpc-id $VPC_ID --query GroupId --output text > /tmp/sg-$n
done
export SG_RDS=$(cat /tmp/sg-rds) SG_REDIS=$(cat /tmp/sg-redis) SG_EFS=$(cat /tmp/sg-efs)
aws ec2 authorize-security-group-ingress --group-id $SG_RDS --protocol tcp --port 1433 \
  --source-group $CLUSTER_SG
aws ec2 authorize-security-group-ingress --group-id $SG_REDIS --protocol tcp --port 6379 \
  --source-group $CLUSTER_SG
aws ec2 authorize-security-group-ingress --group-id $SG_EFS --protocol tcp --port 2049 \
  --source-group $CLUSTER_SG
cat >> ~/mlm-$ENTORNO.env <<EOF
export SG_RDS=$SG_RDS
export SG_REDIS=$SG_REDIS
export SG_EFS=$SG_EFS
EOF
```

### 7.2 Llave KMS del entorno y secretos vacíos

Cifra los secretos, la base, Redis, EFS y S3. La crea `deploy/aws/crear-kms-y-secretos.sh`, que en la misma pasada crea vacíos (`{}`) los 10 secretos de la tabla. Se corre aquí porque RDS, Redis, EFS y S3 necesitan la llave; los secretos se llenan en la Fase 6.

```
cd $REPO
bash deploy/aws/crear-kms-y-secretos.sh $ENTORNO
export KMS_ARN=$(aws kms describe-key --key-id alias/mlmconqueror-$ENTORNO \
  --query KeyMetadata.Arn --output text)
echo "export KMS_ARN=$KMS_ARN" >> ~/mlm-$ENTORNO.env
aws kms get-key-rotation-status --key-id $KMS_ARN     # KeyRotationEnabled: true
```

El script se puede correr las veces que haga falta: si la llave o un secreto ya existen, no los toca y lo dice (`ya existe: ...`). En `staging` la llave y los secretos se crearon el 19-nov-2026, así que todas las líneas dicen `ya existe`.

### 7.3 RDS for SQL Server

**Paso 1.** Elegir la versión 2022 (16.00) más reciente:

```
aws rds describe-db-engine-versions --engine sqlserver-se \
  --query 'DBEngineVersions[?starts_with(EngineVersion,`16.00`)].EngineVersion' --output text
```

Se anota la última que aparece como `RDS_VERSION` en `~/mlm-$ENTORNO.env`.

**Paso 2.** Grupo de subredes y creación de la instancia. La zona horaria **se fija aquí y no se puede cambiar después**; tiene que ser la misma que `TZ_NEGOCIO`, porque el código usa `GETDATE()`.

```
aws rds create-db-subnet-group --db-subnet-group-name mlm-$ENTORNO \
  --db-subnet-group-description "mlm $ENTORNO" --subnet-ids $SUBNETS_PRIVADAS
aws rds create-db-instance --db-instance-identifier mlm-$ENTORNO-sql \
  --engine sqlserver-se --engine-version $RDS_VERSION --license-model license-included \
  --db-instance-class db.m6i.xlarge --allocated-storage 200 --storage-type gp3 \
  --max-allocated-storage 1000 --multi-az \
  --master-username mlmadmin --manage-master-user-password \
  --master-user-secret-kms-key-id $KMS_ARN \
  --db-subnet-group-name mlm-$ENTORNO --vpc-security-group-ids $SG_RDS \
  --no-publicly-accessible --storage-encrypted --kms-key-id $KMS_ARN \
  --backup-retention-period 14 --preferred-backup-window 07:00-08:00 \
  --preferred-maintenance-window sun:08:30-sun:09:30 \
  --timezone "Eastern Standard Time" \
  --enable-performance-insights --performance-insights-kms-key-id $KMS_ARN \
  --deletion-protection --copy-tags-to-snapshot
aws rds wait db-instance-available --db-instance-identifier mlm-$ENTORNO-sql   # 30-60 min
```

La contraseña del usuario maestro la genera RDS y la guarda en Secrets Manager (`--manage-master-user-password`). Nadie la escribe.

**Paso 3.** Anotar el host:

```
export RDS_HOST=$(aws rds describe-db-instances --db-instance-identifier mlm-$ENTORNO-sql \
  --query 'DBInstances[0].Endpoint.Address' --output text)
echo "export RDS_HOST=$RDS_HOST" >> ~/mlm-$ENTORNO.env
```

La base de datos y el usuario de la aplicación se crean en la Fase 8, desde dentro del clúster: la instancia no es accesible desde Internet.

### 7.4 ElastiCache (Redis) con TLS y token

```
openssl rand -base64 48 | tr -d '/+=' | cut -c1-64 > /tmp/redis-token
aws elasticache create-cache-subnet-group --cache-subnet-group-name mlm-$ENTORNO \
  --cache-subnet-group-description "mlm $ENTORNO" --subnet-ids $SUBNETS_PRIVADAS
aws elasticache create-replication-group --replication-group-id mlm-$ENTORNO-redis \
  --replication-group-description "mlm $ENTORNO" --engine redis \
  --cache-node-type cache.t4g.medium --num-node-groups 1 --replicas-per-node-group 1 \
  --automatic-failover-enabled --multi-az-enabled \
  --cache-subnet-group-name mlm-$ENTORNO --security-group-ids $SG_REDIS \
  --transit-encryption-enabled --at-rest-encryption-enabled --kms-key-id $KMS_ARN \
  --auth-token "$(cat /tmp/redis-token)" --snapshot-retention-limit 3
aws elasticache wait replication-group-available --replication-group-id mlm-$ENTORNO-redis
export REDIS_HOST=$(aws elasticache describe-replication-groups \
  --replication-group-id mlm-$ENTORNO-redis \
  --query 'ReplicationGroups[0].NodeGroups[0].PrimaryEndpoint.Address' --output text)
echo "export REDIS_HOST=$REDIS_HOST" >> ~/mlm-$ENTORNO.env
```

`/tmp/redis-token` se usa en la Fase 6 y se borra allí.

### 7.5 EFS (disco compartido) y clase de almacenamiento

```
export EFS_ID=$(aws efs create-file-system --encrypted --kms-key-id $KMS_ARN \
  --performance-mode generalPurpose --throughput-mode elastic \
  --tags Key=Name,Value=mlm-$ENTORNO --query FileSystemId --output text)
echo "export EFS_ID=$EFS_ID" >> ~/mlm-$ENTORNO.env
sleep 20
for s in $SUBNETS_PRIVADAS; do
  aws efs create-mount-target --file-system-id $EFS_ID --subnet-id $s --security-groups $SG_EFS
done
aws efs put-backup-policy --file-system-id $EFS_ID --backup-policy Status=ENABLED
```

Clase de almacenamiento. Los archivos quedan con el usuario 1654, que es el `APP_UID` de las imágenes de .NET:

```
cat > ~/mlm-k8s/$ENTORNO/storageclass.yaml <<'EOF'
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: efs-mlm
provisioner: efs.csi.aws.com
parameters:
  provisioningMode: efs-ap
  fileSystemId: ${EFS_ID}
  directoryPerms: "700"
  uid: "1654"
  gid: "1654"
  basePath: /mlm
reclaimPolicy: Retain
EOF
aplicar ~/mlm-k8s/$ENTORNO/storageclass.yaml
```

**Comprobar:** `aws efs describe-mount-targets --file-system-id $EFS_ID` muestra 3 destinos en estado `available`.

### 7.6 S3

```
aws s3api create-bucket --bucket $BUCKET --region $AWS_REGION
aws s3api put-public-access-block --bucket $BUCKET --public-access-block-configuration \
  BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
aws s3api put-bucket-versioning --bucket $BUCKET --versioning-configuration Status=Enabled
aws s3api put-bucket-encryption --bucket $BUCKET --server-side-encryption-configuration \
  "{\"Rules\":[{\"ApplyServerSideEncryptionByDefault\":{\"SSEAlgorithm\":\"aws:kms\",
  \"KMSMasterKeyID\":\"$KMS_ARN\"},\"BucketKeyEnabled\":true}]}"
aws s3api put-bucket-ownership-controls --bucket $BUCKET \
  --ownership-controls 'Rules=[{ObjectOwnership=BucketOwnerEnforced}]'
```

En `us-east-1` no se pasa `--create-bucket-configuration`. En otra región se agrega `--create-bucket-configuration LocationConstraint=$AWS_REGION`.

### 7.7 SES (correo del 2FA, de la recuperación de contraseña y de los avisos)

**Paso 1.** Identidad del dominio con DKIM:

```
aws sesv2 create-email-identity --email-identity $DOMINIO \
  --query 'DkimAttributes.Tokens' --output text
```

**Paso 2.** Por cada uno de los 3 tokens que devuelve, crear un CNAME `<token>._domainkey.$DOMINIO` que apunte a `<token>.dkim.amazonses.com`. Se hace con un `change-resource-record-sets` igual al de la sección 4.2, con TTL 300.

**Paso 3.** Registros SPF y DMARC:

```
cat > /tmp/ses.json <<EOF
{"Changes":[
 {"Action":"UPSERT","ResourceRecordSet":{"Name":"$DOMINIO","Type":"TXT","TTL":300,
  "ResourceRecords":[{"Value":"\"v=spf1 include:amazonses.com ~all\""}]}},
 {"Action":"UPSERT","ResourceRecordSet":{"Name":"_dmarc.$DOMINIO","Type":"TXT","TTL":300,
  "ResourceRecords":[{"Value":"\"v=DMARC1; p=quarantine; adkim=s; aspf=r\""}]}}]}
EOF
aws route53 change-resource-record-sets --hosted-zone-id $ZONA_ID --change-batch file:///tmp/ses.json
```

Si el dominio ya tiene un registro SPF (`v=spf1`), no se crea otro: se agrega `include:amazonses.com` al que existe.

**Paso 4.** Pedir salir del sandbox. Sin este paso, SES solo entrega a direcciones verificadas y el 2FA falla para todos los demás:

```
aws sesv2 put-account-details --production-access-enabled --mail-type TRANSACTIONAL \
  --website-url "https://app.$DOMINIO" --contact-language EN \
  --use-case-description "Transactional email only: two-factor codes, password reset, \
account notices for registered members of our platform. No marketing."
```

AWS responde en 24 a 48 horas. **Comprobar:** `aws sesv2 get-account` muestra `"ProductionAccessEnabled": true`, y `aws sesv2 get-email-identity --email-identity $DOMINIO` muestra `"VerifiedForSendingStatus": true`.

---

## 8. Fase 6 — Secretos en AWS Secrets Manager y un rol por servicio

**Qué se persigue:** que cada secreto del entorno (cadenas de conexión, llave de firma JWT, certificado del anillo de las pasarelas, claves de terceros) viva en un solo sitio, cifrado, y que cada servicio tenga un rol de AWS que solo puede leer los secretos que necesita.

**Por qué así:**
- **Menor privilegio de verdad:** el rol de Billing puede leer `datos`, `dp-gateway`, `billing`, `firebase` y `twilio`, y nada más. Si alguien toma el control de un pod de Billing, no puede leer la llave de firma JWT, porque ese secreto no está en su fila. La tabla que lo decide es un archivo del repositorio (`deploy/aws/secretos-por-servicio.json`): se revisa en cada cambio como cualquier código, y una prueba automática comprueba que la lista de cada servicio coincide con su fila.
- **Agrupados por tema y no por servicio:** la cadena de SQL vive en un secreto (`datos`) y no copiada en nueve. Cambiarla es cambiar un valor.
- **Un juego por entorno:** los nombres llevan el entorno (`mlmconqueror/$ENTORNO/...`) y los roles también. Un servicio de `staging` no puede leer un secreto de `prod`, aunque alguien se equivoque de configuración, porque su rol no lo alcanza.
- **Cifrados con la llave del entorno:** el rol solo puede usar la llave a través de Secrets Manager. No sirve para descifrar otra cosa.
- **Auditados:** cada lectura queda en CloudTrail con el rol que la hizo.
- **Sin copias:** el valor no pasa por Kubernetes ni por un archivo en el nodo. Va de Secrets Manager a la memoria del servicio.
- **Scripts y no comandos sueltos:** `deploy/aws/` crea las llaves, los secretos y los roles a partir de la tabla. Lo que se ejecuta en `staging` es exactamente lo que se ejecuta en `prod`.

**Alternativas descartadas:**
- **External Secrets Operator con un rol para todo:** un solo rol leía todo `mlmconqueror/prod/*` y copiaba los valores a Secrets de Kubernetes. Quien pudiera leer los Secrets del namespace los tenía todos, y hacían falta 9 manifiestos que repetían la tabla a mano.
- **Secrets de Kubernetes escritos a mano:** el valor queda en el clúster (y en `etcd`) y en el historial de quien lo escribió, sin registro de quién lo leyó.
- **Variables de entorno en los manifiestos o en un `.env`:** el secreto queda en el repositorio o en un disco, y cualquiera que pueda ver la definición del pod lo ve.
- **Un secreto por servicio:** la cadena de SQL repetida en nueve sitios, y nueve sitios que cambiar cuando rota.
- **Parameter Store:** sirve, pero no tiene rotación administrada, y la contraseña maestra de RDS ya vive en Secrets Manager: serían dos sitios para lo mismo.

### 8.1 Qué secretos hay y quién lee cada uno

Todos están bajo `mlmconqueror/$ENTORNO/`. Cada uno es un JSON cuyas claves son **el nombre de la variable de entorno de .NET** (`Seccion__Clave`). El servicio convierte cada una en `Seccion:Clave` al leerla.

| Secreto | Claves | Lo leen |
|---|---|---|
| `datos` | `ConnectionStrings__DefaultConnection`, `ConnectionStrings__HangFire`, `ConnectionStrings__Redis` | AdminAPI, Billing, BizCenter, CommissionEngine, RankEngine, SharedAPICenter, SignupAPI y TicketManagementSystem |
| `jwt-firma` | `Jwt__PrivateKeyBase64` | AdminAPI y SignupAPI, y nadie más |
| `dp-gateway` | `DataProtection__Certificate__Base64`, `DataProtection__Certificate__Password` | AdminAPI, Billing y BizCenter |
| `signupapi` | `RootAmbassador__Email`, `RootAmbassador__Password`, `Entrax__KeyId`, `Entrax__Secret` | SignupAPI |
| `billing` | `Stripe__SecretKey` | Billing |
| `sharedapicenter` | `ExternalApi__ApiKey` | SharedAPICenter |
| `portales` | `Syncfusion__LicenseKey` | AdminWeb y BizCenterWeb |
| `firebase` | `Firebase__CredentialsJsonBase64` | Billing, BizCenter, CommissionEngine, RankEngine, SharedAPICenter, SignupAPI y TicketManagementSystem |
| `twilio` | `Notifications__Sms__Twilio__AccountSid`, `Notifications__Sms__Twilio__AuthToken` | AdminAPI, Billing, BizCenter, RankEngine y SignupAPI |
| `borde` | `Cloudflare__TunnelToken` | Ningún servicio. Solo se usa en el despliegue con Docker; en EKS queda vacío |

Reglas:
- **Signups no lee ningún secreto.** Su lista está vacía y no llama a AWS.
- **Lo que no es secreto no va aquí:** la llave pública JWT, las URL, CORS, la zona horaria y los puertos van en los ConfigMaps (Fase 10).
- **S3 y SES no necesitan secreto:** usan el rol del servicio. Los tienen AdminAPI, Billing, BizCenter, RankEngine y SignupAPI.
- **Un secreto vacío (`{}`) es válido:** el servicio lo lee, carga 0 claves y sigue. Así quedan `billing` (sin cuenta de Stripe), `twilio` (hasta la Fase 15) y `firebase` si no hay proyecto de Firebase.
- **Durante una rotación del PFX** conviven el certificado nuevo y los viejos: los retirados van en el mismo `dp-gateway` como `DataProtection__Certificate__Retired__0__Base64` y `DataProtection__Certificate__Retired__0__Password` (y `__1__`, `__2__`...).
- **La tabla vive en `deploy/aws/secretos-por-servicio.json`.** Dar un secreto a un servicio es cambiar ese archivo y la lista `SecretosAws:Secretos` del `appsettings.json` del servicio, y volver a correr `crear-roles.sh` (sección 8.8).

### 8.2 Carpeta de trabajo temporal

Todo lo que se genera en esta fase va a una carpeta que solo lee el usuario y que se borra al final.

```
umask 077
mkdir -p ~/mlm-secretos && cd ~/mlm-secretos
set +o history      # no guardar en el historial lo que se escriba en esta fase
C=$REPO/deploy/aws/cargar-secreto.sh
```

`cargar-secreto.sh <entorno> <secreto> <archivo.json>` comprueba que el secreto esté en la tabla y que el archivo sea un objeto JSON con valores de texto, carga el valor y lista los servicios que lo leen. **Reemplaza el valor entero:** el JSON lleva siempre todas las claves del secreto.

### 8.3 Llaves JWT

Formato **PKCS#8 DER en base64**, de 2048 bits. Es el mismo procedimiento de `docs/deployment/rotacion-llaves-jwt.md`. No se usa PEM ni `genrsa`.

En `staging` el par ya existe: la privada está en `mlmconqueror/staging/jwt-firma` y la pública en `deploy/aws/llaves-publicas/staging-jwt-publica.b64`. Si `jwt-firma` ya tiene valor, no se genera otro par; solo se anota la pública:

```
aws secretsmanager get-secret-value --secret-id mlmconqueror/$ENTORNO/jwt-firma \
  --query SecretString --output text | jq 'keys'     # ["Jwt__PrivateKeyBase64"] = ya tiene valor
echo "export JWT_PUBLICA=$(cat $REPO/deploy/aws/llaves-publicas/staging-jwt-publica.b64)" \
  >> ~/mlm-$ENTORNO.env                                # solo en staging, si ya tiene valor
```

En `prod` (y en `staging` si el secreto está vacío) se genera el par:

```
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out jwt-privada.pem
openssl pkcs8 -topk8 -nocrypt -in jwt-privada.pem -outform DER -out jwt-privada.der
openssl asn1parse -inform DER -in jwt-privada.der | head -4   # debe aparecer rsaEncryption
openssl rsa -inform DER -in jwt-privada.der -pubout -outform DER -out jwt-publica.der
base64 -w0 jwt-privada.der > jwt-privada.b64
base64 -w0 jwt-publica.der > jwt-publica.b64
```

El paso por `pkcs8 -topk8` no es opcional: el openssl de Git para Windows saca la llave en PKCS#1 aunque se use `genpkey`, y SignupAPI no arranca con ella. Si en la salida de `asn1parse` no aparece `rsaEncryption`, la llave no sirve. Al terminar se borra `jwt-privada.pem` junto con los demás archivos de esta fase.

La pública no es secreta: va a la configuración de todos los servicios (Fase 10).

```
echo "export JWT_PUBLICA=$(cat jwt-publica.b64)" >> ~/mlm-$ENTORNO.env
```

### 8.4 Certificado del anillo de credenciales de las pasarelas

AdminAPI cifra con él las credenciales de PayQuicker, i-Payout y Volet. Billing y BizCenter las descifran. Los tres tienen que tener **el mismo PFX**: si se pierde, todas las credenciales guardadas quedan ilegibles. Cada entorno tiene el suyo.

```
openssl rand -base64 32 | tr -d '/+=' > pfx-pass
openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
  -keyout dp.key -out dp.crt -subj "/CN=MLMConqueror Gateway Credentials" \
  -addext "keyUsage=critical,keyEncipherment,dataEncipherment"
openssl pkcs12 -export -inkey dp.key -in dp.crt -out gateway.pfx \
  -passout file:pfx-pass -certpbe AES-256-CBC -keypbe AES-256-CBC -macalg sha256
base64 -w0 gateway.pfx > gateway.pfx.b64
```

**Antes de seguir:** se guarda una copia de `gateway.pfx` y `pfx-pass` fuera de AWS, en el gestor de contraseñas de la empresa, con el nombre del entorno. Es la única forma de recuperar las credenciales si alguien borra el secreto.

### 8.5 Base de datos: contraseña del usuario de la aplicación

```
openssl rand -base64 36 | tr -d '/+=;' | cut -c1-40 > sql-app-pass
```

### 8.6 Cargar los secretos

Los secretos ya existen, vacíos, desde la sección 7.2. Por cada uno se arma su JSON y se carga.

**Datos.** Cadena de conexión con cifrado y validación del certificado (la CA de RDS va en la imagen):

```
SQL="Server=$RDS_HOST,1433;Database=MLMConquerorGlobalEditionDb;User Id=mlm_app;\
Password=$(cat sql-app-pass);Encrypt=True;TrustServerCertificate=False;\
MultipleActiveResultSets=True;Connect Timeout=30"
REDIS="$REDIS_HOST:6379,ssl=True,password=$(cat /tmp/redis-token),abortConnect=False"
jq -n --arg s "$SQL" --arg r "$REDIS" '{
  "ConnectionStrings__DefaultConnection": $s,
  "ConnectionStrings__HangFire": $s,
  "ConnectionStrings__Redis": $r }' > datos.json
bash $C $ENTORNO datos datos.json
```

**Llave de firma JWT** (se omite en `staging` si ya tenía valor, sección 8.3):

```
jq -n --arg k "$(cat jwt-privada.b64)" '{"Jwt__PrivateKeyBase64": $k}' > jwt.json
bash $C $ENTORNO jwt-firma jwt.json
```

La salida tiene que listar solo AdminAPI y SignupAPI.

**SignupAPI.** La cuenta raíz que el arranque siembra. El correo es un buzón real de la empresa, nunca uno público. Los datos de Entrax quedan vacíos hasta la Fase 15:

```
read -p "Correo de la cuenta raiz: " ROOT_MAIL
openssl rand -base64 24 | tr -d '/+=' > root-pass
jq -n --arg e "$ROOT_MAIL" --arg p "$(cat root-pass)Aa1#" '{
  "RootAmbassador__Email": $e, "RootAmbassador__Password": $p,
  "Entrax__KeyId": "", "Entrax__Secret": "" }' > signupapi.json
bash $C $ENTORNO signupapi signupapi.json
```

La contraseña de la cuenta raíz (el contenido de `root-pass` seguido de `Aa1#`) se guarda en el gestor de contraseñas y se cambia en el primer inicio de sesión.

**Anillo de las pasarelas.** El PFX va entero, en base64, dentro del secreto: no se monta ningún archivo.

```
jq -n --arg b "$(cat gateway.pfx.b64)" --arg p "$(cat pfx-pass)" '{
  "DataProtection__Certificate__Base64": $b,
  "DataProtection__Certificate__Password": $p }' > dp.json
bash $C $ENTORNO dp-gateway dp.json
```

**API externa (`X-Api-Key`):**

```
jq -n --arg k "$(openssl rand -hex 32)" '{"ExternalApi__ApiKey": $k}' > ext.json
bash $C $ENTORNO sharedapicenter ext.json
```

**Portales (Syncfusion):**

```
read -s -p "Clave de licencia de Syncfusion: " SF; echo
jq -n --arg k "$SF" '{"Syncfusion__LicenseKey": $k}' > portales.json
bash $C $ENTORNO portales portales.json
```

**Firebase.** Se copia antes a `~/mlm-secretos/firebase.json` el JSON de la cuenta de servicio, descargado de la consola de Firebase (Configuración del proyecto, Cuentas de servicio). Va en base64:

```
jq -n --arg j "$(base64 -w0 firebase.json)" '{"Firebase__CredentialsJsonBase64": $j}' > fb.json
bash $C $ENTORNO firebase fb.json
```

Si no hay proyecto de Firebase, este secreto se queda vacío (`{}`): los servicios arrancan y el push queda apagado.

**Billing y Twilio** se quedan vacíos (`{}`): Stripe no se usa y Twilio se carga en la Fase 15. **`borde`** también se queda vacío: es del despliegue con Docker.

### 8.7 Borrar la carpeta de trabajo

```
cd ~ && shred -u ~/mlm-secretos/* /tmp/redis-token 2>/dev/null; rm -rf ~/mlm-secretos
unset SF SQL REDIS ROOT_MAIL C
set -o history
for s in $(jq -r '.secretos[]' $REPO/deploy/aws/secretos-por-servicio.json); do
  printf "%-16s" $s
  aws secretsmanager get-secret-value --secret-id mlmconqueror/$ENTORNO/$s \
    --query SecretString --output text | jq -c 'keys'
done
```

**Comprobar:** cada secreto muestra los nombres de sus claves (nunca los valores) según la tabla de la sección 8.1; `billing`, `twilio` y `borde` muestran `[]`, y `firebase` también si no hay proyecto de Firebase. La contraseña de `mlm_app` hace falta en la Fase 8, y se lee del secreto `datos` en ese momento.

### 8.8 Un rol por servicio

`deploy/aws/crear-roles.sh` crea, por cada servicio de la tabla, el rol `MLM-$ENTORNO-<servicio>` (en minúsculas: `MLM-prod-billing`), con:
- **Confianza:** solo lo puede asumir la cuenta de servicio de Kubernetes con el mismo nombre (`system:serviceaccount:$NS:billing`) de este clúster, por IRSA.
- **Permisos:** `secretsmanager:GetSecretValue` solo sobre los secretos de su fila; `kms:Decrypt` sobre la llave del entorno, solo a través de Secrets Manager; y, si los usa, S3 sobre `$BUCKET` y SES solo desde `$SES_FROM`.

Lee `BUCKET` y `SES_FROM` del archivo de variables:

```
cd $REPO
BUCKET=$BUCKET SES_FROM=$SES_FROM bash deploy/aws/crear-roles.sh $ENTORNO eks $CLUSTER $NS
```

Imprime una línea por servicio, por ejemplo `MLM-prod-billing: 5 secreto(s); aws: s3 ses`. El de Signups queda sin permisos (`0 secreto(s); aws: ninguno`).

El bucket está cifrado con la llave del entorno, así que los cinco servicios con S3 necesitan además usar esa llave **a través de S3**. Se agrega como una política aparte, que `crear-roles.sh` no toca cuando se vuelve a correr:

```
for s in $(jq -r '.servicios | to_entries[] | select(.value.aws | index("s3")) | .key' \
           deploy/aws/secretos-por-servicio.json); do
  n=$(echo "$s" | tr '[:upper:]' '[:lower:]')
  jq -n --arg kms "$KMS_ARN" --arg via "s3.$AWS_REGION.amazonaws.com" '{
    Version:"2012-10-17", Statement:[{Effect:"Allow",
      Action:["kms:Decrypt","kms:GenerateDataKey"], Resource:$kms,
      Condition:{StringEquals:{"kms:ViaService":$via}}}]}' > /tmp/s3-kms.json
  aws iam put-role-policy --role-name MLM-$ENTORNO-$n --policy-name s3-kms \
    --policy-document file:///tmp/s3-kms.json
  echo "MLM-$ENTORNO-$n: s3-kms"
done
rm -f /tmp/s3-kms.json
```

**Comprobar:**

```
aws iam list-roles --query "Roles[?starts_with(RoleName,'MLM-$ENTORNO-')].RoleName" --output text
aws iam get-role-policy --role-name MLM-$ENTORNO-billing --policy-name secretos-y-aws \
  --query 'PolicyDocument.Statement[0].Resource'
```

El primero lista 11 roles. El segundo muestra los cinco secretos de Billing y no muestra `jwt-firma`.

Cuando se cambia la tabla (un secreto nuevo para un servicio), se vuelve a correr `crear-roles.sh` con los mismos argumentos y se reinicia ese servicio.

---

## 9. Fase 7 — ECR e imágenes

**Qué se persigue:** construir las 11 imágenes a partir del commit anotado en la Fase 0 y guardarlas en un registro privado de la cuenta, cada una con la etiqueta del commit del que sale.

**Por qué así:**
- **ECR privado, en la misma cuenta:** los nodos bajan las imágenes por la red de AWS y con su propio rol; no hay contraseña de registro que guardar.
- **Etiquetas inmutables con el commit corto:** una etiqueta no se puede sobrescribir, así que lo que corre en el clúster es siempre lo que dice la etiqueta, y deshacer un despliegue es volver a la etiqueta anterior.
- **Análisis de vulnerabilidades al subir** y política que borra las imágenes viejas (se guardan las 30 últimas).
- **Una sola receta (`Dockerfile`) para los 11 servicios:** la misma base, los mismos parches y la misma CA de RDS en todos.
- **Las imágenes no llevan secretos ni el nombre del entorno:** la misma imagen corre en `staging` y en `prod`; el entorno lo dice el ConfigMap (Fase 10).

**Alternativas descartadas:**
- **Docker Hub o GitHub Container Registry:** otra credencial que guardar en el clúster y las imágenes fuera de la cuenta.
- **Etiqueta `latest`:** no se sabe qué código corre ni a qué volver si algo falla.
- **Una imagen por entorno:** lo que se probó en `staging` no sería lo que llega a `prod`.

### 9.1 Repositorios

```
SERVICIOS="signupapi adminapi billing bizcenter commissionengine rankengine \
ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups"
echo "export SERVICIOS=\"$SERVICIOS\"" >> ~/mlm-$ENTORNO.env
for s in $SERVICIOS; do
  aws ecr create-repository --repository-name mlmconqueror/$s \
    --image-tag-mutability IMMUTABLE --image-scanning-configuration scanOnPush=true \
    --encryption-configuration encryptionType=KMS,kmsKey=$KMS_ARN
  aws ecr put-lifecycle-policy --repository-name mlmconqueror/$s --lifecycle-policy-text \
    '{"rules":[{"rulePriority":1,"description":"ultimas 30","selection":{"tagStatus":"any",
    "countType":"imageCountMoreThan","countNumber":30},"action":{"type":"expire"}}]}'
done
```

### 9.2 Nombre del proyecto de cada servicio

```
proyecto() { case $1 in
  signupapi) echo SignupAPI;; adminapi) echo AdminAPI;; billing) echo Billing;;
  bizcenter) echo BizCenter;; commissionengine) echo CommissionEngine;;
  rankengine) echo RankEngine;; ticketmanagementsystem) echo TicketManagementSystem;;
  sharedapicenter) echo SharedAPICenter;; adminweb) echo AdminWeb;;
  bizcenterweb) echo BizCenterWeb;; signups) echo Signups;; esac; }
declare -f proyecto >> ~/mlm-$ENTORNO.env
```

### 9.3 Construir y subir las 11 imágenes

La etiqueta es el commit corto. Así cada imagen dice de qué código sale.

```
cd $REPO && git pull && git status --short     # sin cambios locales
export TAG=$(git rev-parse --short=7 HEAD)
echo "export TAG=$TAG" >> ~/mlm-$ENTORNO.env
aws ecr get-login-password | docker login --username AWS --password-stdin $ECR
for s in $SERVICIOS; do
  P=MLMConquerorGlobalEdition.$(proyecto $s)
  docker build --build-arg PROJECT=$P -t $ECR/mlmconqueror/$s:$TAG . || break
  docker push $ECR/mlmconqueror/$s:$TAG || break
done
```

**Comprobar:**

```
for s in $SERVICIOS; do
  aws ecr describe-image-scan-findings --repository-name mlmconqueror/$s --image-id imageTag=$TAG \
    --query 'imageScanFindings.findingSeverityCounts' --output json
done
```

Cada servicio tiene que tener su imagen. Si alguna muestra hallazgos `CRITICAL`, se revisan antes de seguir.

---

## 10. Fase 8 — Base de datos: creación y migraciones

**Qué se persigue:** crear la base de datos y el usuario con el que se conectan los servicios, y dejar el esquema al día antes de que arranque ningún servicio.

**Por qué así:**
- **Desde dentro del clúster:** la base no es accesible desde internet, así que el trabajo se hace desde un pod temporal que se borra al final. No se abre ningún puerto, ni siquiera por un rato.
- **La contraseña maestra la lee de Secrets Manager** quien hace el despliegue, en el momento; la del usuario de la aplicación sale del secreto `datos`. Ninguna se escribe.
- **Un usuario de aplicación (`mlm_app`) distinto del maestro:** es dueño de su base y de nada más. Si se filtra, no alcanza a otras bases ni a la administración de la instancia.
- **Un script de migraciones idempotente**, aplicado una vez y antes de los servicios: si varias réplicas migraran a la vez al arrancar, podrían chocar.
- **Una instantánea justo después:** el punto de partida queda guardado.

**Alternativas descartadas:**
- **Abrir la base a la IP de la oficina** para conectarse con SQL Server Management Studio: deja la base expuesta a internet y depende de que alguien se acuerde de cerrarla.
- **Usar el usuario maestro en las cadenas de conexión:** cualquier fallo en un servicio daría control total de la instancia.
- **Dejar que cada servicio migre al arrancar como único mecanismo:** sin control de orden ni de cuándo cambia el esquema.

### 10.1 Script de migraciones del commit que se despliega

`DeploymentScripts/migrate-rds.sql` es idempotente (cada migración comprueba `__EFMigrationsHistory`). En `17197f4` llega hasta `20261114211022_ReconocimientoDeClubes`. Si el commit de la Fase 0 agregó migraciones, hay que regenerarlo y hacer commit:

```
cd $REPO
dotnet ef migrations list --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.SignupAPI --no-connect | tail -1
grep -c "20261114211022_ReconocimientoDeClubes" DeploymentScripts/migrate-rds.sql
```

Si la última migración que lista el primer comando no aparece en el script:

```
dotnet ef migrations script --idempotent --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.SignupAPI -o DeploymentScripts/migrate-rds.sql
```

### 10.2 Pod temporal con sqlcmd dentro del clúster

```
kubectl run sqlcmd -n $NS --image=mcr.microsoft.com/mssql/server:2022-latest \
  --restart=Never --command -- sleep 7200
kubectl wait -n $NS --for=condition=Ready pod/sqlcmd --timeout=300s
kubectl exec -n $NS sqlcmd -- ls /opt/mssql-tools18/bin/sqlcmd
```

Contraseña maestra, que RDS guardó en Secrets Manager:

```
MASTER_SECRET=$(aws rds describe-db-instances --db-instance-identifier mlm-$ENTORNO-sql \
  --query 'DBInstances[0].MasterUserSecret.SecretArn' --output text)
MASTER_PASS=$(aws secretsmanager get-secret-value --secret-id $MASTER_SECRET \
  --query SecretString --output text | jq -r .password)
APP_PASS=$(aws secretsmanager get-secret-value --secret-id mlmconqueror/$ENTORNO/datos \
  --query SecretString --output text | jq -r .ConnectionStrings__DefaultConnection \
  | sed -E 's/.*Password=([^;]*);.*/\1/')
sq() { kubectl exec -i -n $NS sqlcmd -- /opt/mssql-tools18/bin/sqlcmd -S "$RDS_HOST,1433" \
  -U mlmadmin -P "$MASTER_PASS" -N -C -b "$@"; }
```

### 10.3 Crear la base y el usuario de la aplicación

```
sq -Q "CREATE DATABASE MLMConquerorGlobalEditionDb;"
sq -Q "ALTER DATABASE MLMConquerorGlobalEditionDb \
  SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;"
sq -Q "CREATE LOGIN mlm_app WITH PASSWORD = N'$APP_PASS', CHECK_POLICY = ON;"
sq -d MLMConquerorGlobalEditionDb -Q "CREATE USER mlm_app FOR LOGIN mlm_app; \
ALTER ROLE db_owner ADD MEMBER mlm_app;"
```

`mlm_app` es `db_owner` de su base y de nada más. Lo necesita porque los servicios ejecutan `MigrateAsync()` al arrancar y Hangfire crea su esquema.

### 10.4 Aplicar las migraciones

```
kubectl cp $REPO/DeploymentScripts/migrate-rds.sql $NS/sqlcmd:/tmp/migrate.sql
sq -d MLMConquerorGlobalEditionDb -i /tmp/migrate.sql -o /tmp/migrate.log
kubectl exec -n $NS sqlcmd -- tail -5 /tmp/migrate.log
sq -d MLMConquerorGlobalEditionDb -h -1 -Q \
  "SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;"
```

**Comprobar:** la última fila es la última migración del código. `-b` corta en el primer error; si corta, se lee `/tmp/migrate.log`, se corrige la causa y se vuelve a ejecutar, porque el script es idempotente.

### 10.5 Instantánea inicial y limpieza

```
aws rds create-db-snapshot --db-instance-identifier mlm-$ENTORNO-sql \
  --db-snapshot-identifier mlm-$ENTORNO-sql-esquema-$TAG
kubectl delete pod -n $NS sqlcmd
unset MASTER_PASS APP_PASS
```

---

## 11. Fase 9 — Identidad de AWS de cada pod y volúmenes

**Qué se persigue:** que cada servicio tenga en Kubernetes su propia cuenta de servicio, unida a su propio rol de AWS (el de la sección 8.8), y que los servicios que escriben en disco tengan sus volúmenes compartidos en EFS.

**Por qué así:**
- **IRSA (IAM Roles for Service Accounts):** EKS entrega a cada pod un token firmado que dice "soy la cuenta de servicio `billing` del namespace `mlmconqueror` de este clúster". El SDK de AWS lo cambia por credenciales temporales del rol `MLM-$ENTORNO-billing`, que caducan en una hora y se renuevan solas. No hay llaves de acceso en ningún sitio.
- **Una cuenta de servicio por Deployment:** el rol de Billing solo lo puede asumir el pod de Billing. Un pod comprometido solo obtiene los permisos de su propio servicio.
- **El token de la API de Kubernetes no se monta en los pods:** ningún servicio habla con Kubernetes, así que no lo necesitan. El token de IRSA es otro, con audiencia `sts.amazonaws.com`, y solo sirve para pedir credenciales de AWS.
- **EFS con acceso `ReadWriteMany`:** varias réplicas de un servicio, en zonas distintas, comparten las mismas llaves de Data Protection y los mismos adjuntos.

**Alternativas descartadas:**
- **Una cuenta de servicio compartida con un rol para todos:** todos los servicios tendrían los permisos del que más necesita, incluida la lectura de todos los secretos.
- **Llaves de acceso de un usuario de IAM en variables de entorno:** valen hasta que alguien las rota, se pueden copiar y no dicen qué pod las usó.
- **El rol del nodo (perfil de instancia de EC2):** todos los pods del nodo comparten sus permisos.
- **EKS Pod Identity:** es la alternativa más nueva a IRSA y también sirve. Se eligió IRSA porque el mismo rol con el mismo esquema de confianza ya se usa para el AWS Load Balancer Controller y lo crea `crear-roles.sh`.

### 11.1 Una cuenta de servicio por Deployment, anotada con su rol

El nombre de la cuenta de servicio es el del servicio en minúsculas, el mismo que espera la confianza del rol:

```
cd $REPO
for s in $(jq -r '.servicios | keys[]' deploy/aws/secretos-por-servicio.json); do
  n=$(echo "$s" | tr '[:upper:]' '[:lower:]')
  kubectl create serviceaccount "$n" -n $NS --dry-run=client -o yaml | kubectl apply -f -
  kubectl annotate serviceaccount "$n" -n $NS --overwrite \
    eks.amazonaws.com/role-arn=arn:aws:iam::$CUENTA:role/MLM-$ENTORNO-$n
done
kubectl get serviceaccount -n $NS \
  -o custom-columns=NOMBRE:.metadata.name,ROL:.metadata.annotations.eks\\.amazonaws\\.com/role-arn
```

**Comprobar:** la lista muestra las 11 cuentas (además de `default`), cada una con `arn:aws:iam::<cuenta>:role/MLM-<entorno>-<servicio>`.

En ningún servicio se configuran `AWS__AccessKey`, `AWS__SecretKey` ni `AWS__Credentials__*`. Vacíos, el SDK usa la identidad del pod.

**Prueba de que cada rol solo alcanza lo suyo.** Un pod con la cuenta de servicio de Billing intenta leer la llave de firma JWT, que no está en su fila:

```
kubectl run prueba-billing -n $NS --rm -i --restart=Never --image=amazon/aws-cli \
  --overrides='{"spec":{"serviceAccountName":"billing"}}' -- \
  secretsmanager get-secret-value --region $AWS_REGION \
  --secret-id mlmconqueror/$ENTORNO/jwt-firma --query Name
```

Tiene que fallar con `AccessDeniedException`. La misma prueba con `--secret-id mlmconqueror/$ENTORNO/datos` tiene que devolver el nombre del secreto.

### 11.2 Volúmenes en EFS

Se crea `~/mlm-k8s/$ENTORNO/volumenes.yaml` con 7 PVC: `uploads` (adjuntos de AdminAPI y BizCenter), `recibos` (Billing y AdminAPI), `certificados` (RankEngine) y una de llaves por cada servicio que protege cookies:

```
cat > ~/mlm-k8s/$ENTORNO/volumenes.yaml <<'EOF'
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: uploads, namespace: ${NS}}
spec: {accessModes: [ReadWriteMany], storageClassName: efs-mlm,
       resources: {requests: {storage: 50Gi}}}
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: recibos, namespace: ${NS}}
spec: {accessModes: [ReadWriteMany], storageClassName: efs-mlm,
       resources: {requests: {storage: 10Gi}}}
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: certificados, namespace: ${NS}}
spec: {accessModes: [ReadWriteMany], storageClassName: efs-mlm,
       resources: {requests: {storage: 20Gi}}}
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: llaves-signupapi, namespace: ${NS}}
spec: {accessModes: [ReadWriteMany], storageClassName: efs-mlm,
       resources: {requests: {storage: 1Gi}}}
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: llaves-adminweb, namespace: ${NS}}
spec: {accessModes: [ReadWriteMany], storageClassName: efs-mlm,
       resources: {requests: {storage: 1Gi}}}
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: llaves-bizcenterweb, namespace: ${NS}}
spec: {accessModes: [ReadWriteMany], storageClassName: efs-mlm,
       resources: {requests: {storage: 1Gi}}}
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: llaves-signups, namespace: ${NS}}
spec: {accessModes: [ReadWriteMany], storageClassName: efs-mlm,
       resources: {requests: {storage: 1Gi}}}
EOF
aplicar ~/mlm-k8s/$ENTORNO/volumenes.yaml
kubectl get pvc -n $NS      # las 7 en Bound
```

En EFS el tamaño pedido es solo un requisito de la API, no un límite: se paga lo que se usa.

---

## 12. Fase 10 — Configuración de los servicios (ConfigMaps)

**Qué se persigue:** dar a cada servicio la configuración de este entorno que no es secreta: el entorno de sus secretos, las direcciones públicas e internas, CORS, la zona horaria, la llave pública JWT, los interruptores de producción y cuántos proxies hay delante.

**Por qué así:**
- **Separar lo secreto de lo que no lo es:** lo que va en un ConfigMap se puede leer, revisar y comparar entre entornos sin riesgo. Lo secreto está solo en Secrets Manager.
- **Un ConfigMap común y uno por servicio:** lo que es igual para todos se escribe una vez.
- **`SecretosAws__Entorno: ${ENTORNO}`** es lo único que hace que un servicio lea los secretos de `staging` o de `prod`. El `appsettings.json` del repositorio dice `local`, así que una imagen sin este ConfigMap nunca lee secretos de producción. Y aunque alguien pusiera `prod` en `staging`, el rol del pod no alcanza los secretos de otra cuenta.
- **`ForwardedHeaders__ForwardLimit: "2"`** dice que hay dos proxies de confianza delante del servicio: CloudFront y el ALB. Con `KnownNetwork` igual al CIDR de la VPC, el servicio se queda con la IP que agregó CloudFront, que es la del cliente. Con 1 vería la IP de CloudFront y el límite de peticiones por IP trataría a todos los clientes como uno solo.

**Alternativas descartadas:**
- **Un `appsettings.Production.json` dentro de la imagen:** obliga a reconstruir para cambiar una URL, y `staging` y `prod` necesitarían imágenes distintas.
- **Poner toda la configuración, secreta o no, en Secrets Manager:** se pierde la posibilidad de revisarla a la vista y cada cambio de URL pasaría por el mismo control que una contraseña.
- **Confiar en cualquier proxy (`ForwardLimit` alto o sin `KnownNetwork`):** el cliente podría escribir la IP que quisiera en `X-Forwarded-For` y saltarse el límite por IP.

Solo se sobrescribe lo que en producción tiene que ser distinto de `appsettings.json`. Lo demás (Entrax `Countries`, `IpRateLimiting:GeneralRules`, límites del 2FA, `MyLinks`, etc.) queda como viene en el repositorio.

Hay cuatro reglas que no se cambian:
- `AllowedHosts=*`: el ALB y Kubernetes comprueban la salud del pod con su IP como `Host`. Con `localhost` (el valor del repositorio) responderían 400 y los pods nunca quedarían sanos. El filtro de nombres lo hace el ALB.
- `Signup__CardEnabled`, `Membership__UpgradeEnabled`, `Billing__TokenPurchase__CardEnabled` y `Billing__Recurring__CardEnabled` van en `false`. Son bloqueantes de producción (`docs/deployment/pruebas-de-seguridad.md`).
- Una lista de `appsettings.json` se sobrescribe por posición (`__0`, `__1`, ...), y las posiciones que no se definen se quedan con el valor del repositorio. Por eso `Cors__AllowedOrigins` repite el origen de producción en todas las posiciones que trae el `appsettings.json`: si no, seguirían permitidos los `https://localhost:...` de desarrollo.
- `Auth__TwoFactor__MandatoryRoles` no se define en el primer despliegue. Se enciende en la Fase 15, siguiendo `docs/deployment/verificacion-2fa.md`.

```
cat > ~/mlm-k8s/$ENTORNO/configmaps.yaml <<'EOF'
apiVersion: v1
kind: ConfigMap
metadata: {name: comun, namespace: ${NS}}
data:
  ASPNETCORE_ENVIRONMENT: Production
  TZ: ${TZ_NEGOCIO}
  AllowedHosts: "*"
  SecretosAws__Entorno: ${ENTORNO}
  SecretosAws__Region: ${AWS_REGION}
  ForwardedHeaders__KnownNetwork: ${VPC_CIDR}
  ForwardedHeaders__ForwardLimit: "2"
  IpRateLimiting__RealIpHeader: ""
  Jwt__Issuer: MLMConquerorGlobalEdition
  Jwt__Audience: MLMConquerorGlobalEdition.Clients
  Jwt__PublicKeyBase64: ${JWT_PUBLICA}
  Notifications__Email__Provider: Ses
  Notifications__Email__Ses__Region: ${AWS_REGION}
  Notifications__Email__Ses__FromAddress: ${SES_FROM}
  Notifications__Email__Ses__FromName: MLM Conqueror
  Notifications__Sms__Provider: "Null"
  Logging__LogLevel__Default: Information
---
apiVersion: v1
kind: ConfigMap
metadata: {name: signupapi, namespace: ${NS}}
data:
  Cache__Mode: Required
  Hangfire__WorkerCount: "5"
  Jwt__AccessTokenExpiryMinutes: "15"
  Jwt__RefreshTokenExpiryMinutes: "30"
  Cors__AllowedOrigins__0: https://signup.${DOMINIO}
  Cors__AllowedOrigins__1: https://signup.${DOMINIO}
  Cors__AllowedOrigins__2: https://signup.${DOMINIO}
  Auth__PortalBaseUrl: https://app.${DOMINIO}
  Auth__AdminPortalBaseUrl: https://admin.${DOMINIO}
  Signup__CardEnabled: "false"
  Membership__UpgradeEnabled: "false"
  Entrax__Enabled: "false"
  Entrax__UseFake: "false"
  Entrax__CallbackUrl: https://auth-api.${DOMINIO}/api/v1/signups/entrax/callback
  AWS__S3__BucketName: ${BUCKET}
  AWS__S3__Region: ${AWS_REGION}
  DataProtection__KeysPath: /keys
---
apiVersion: v1
kind: ConfigMap
metadata: {name: adminapi, namespace: ${NS}}
data:
  Cache__Mode: Required
  Cors__AllowedOrigins__0: https://admin.${DOMINIO}
  Cors__AllowedOrigins__1: https://admin.${DOMINIO}
  Services__SignupAPI: http://signupapi.${NS}.svc.cluster.local/
  Services__BillingBaseUrl: http://billing.${NS}.svc.cluster.local/
  Services__Billing: http://billing.${NS}.svc.cluster.local
  Services__BizCenter: http://bizcenter.${NS}.svc.cluster.local
  Services__CommissionEngine: http://commissionengine.${NS}.svc.cluster.local
  Services__RankEngine: http://rankengine.${NS}.svc.cluster.local
  Services__TicketManagementSystem: http://ticketmanagementsystem.${NS}.svc.cluster.local
  Services__SharedAPICenter: http://sharedapicenter.${NS}.svc.cluster.local
  ReceiptStorage__LocalPath: /data/payout-receipts
  ReceiptStorage__PublicBaseUrl: https://admin-api.${DOMINIO}
  Billing__TokenPurchase__CardEnabled: "false"
  Billing__Recurring__CardEnabled: "false"
---
apiVersion: v1
kind: ConfigMap
metadata: {name: billing, namespace: ${NS}}
data:
  ReceiptStorage__LocalPath: /data/payout-receipts
  ReceiptStorage__PublicBaseUrl: https://admin-api.${DOMINIO}
  Billing__TokenPurchase__CardEnabled: "false"
  Billing__Recurring__CardEnabled: "false"
---
apiVersion: v1
kind: ConfigMap
metadata: {name: bizcenter, namespace: ${NS}}
data:
  Cache__Mode: Required
  Hangfire__WorkerCount: "5"
  Cors__AllowedOrigins__0: https://app.${DOMINIO}
  Cors__AllowedOrigins__1: https://app.${DOMINIO}
  Services__RankEngineBaseUrl: http://rankengine.${NS}.svc.cluster.local/
  Services__BillingBaseUrl: http://billing.${NS}.svc.cluster.local/
  AWS__S3__BucketName: ${BUCKET}
  AWS__S3__Region: ${AWS_REGION}
---
apiVersion: v1
kind: ConfigMap
metadata: {name: rankengine, namespace: ${NS}}
data:
  AWS__S3__BucketName: ${BUCKET}
  AWS__S3__Region: ${AWS_REGION}
  CertificateStorage__Provider: Local
  CertificateStorage__LocalPath: /app/wwwroot/certificates
  CertificateStorage__PublicBaseUrl: https://rank.${DOMINIO}
---
apiVersion: v1
kind: ConfigMap
metadata: {name: adminweb, namespace: ${NS}}
data:
  ApiBaseUrl: http://adminapi.${NS}.svc.cluster.local
  AuthApiBaseUrl: http://signupapi.${NS}.svc.cluster.local
  RankEngineApiBaseUrl: http://rankengine.${NS}.svc.cluster.local
  HelpdeskApiUrl: http://ticketmanagementsystem.${NS}.svc.cluster.local
  SignOut__AllowedReturnUrls__0: https://signup.${DOMINIO}/
  DataProtection__KeysPath: /keys
---
apiVersion: v1
kind: ConfigMap
metadata: {name: bizcenterweb, namespace: ${NS}}
data:
  ApiBaseUrl: http://bizcenter.${NS}.svc.cluster.local
  AuthApiBaseUrl: http://signupapi.${NS}.svc.cluster.local
  SignupAppUrl: https://signup.${DOMINIO}/ambassador-join
  SignOut__AllowedReturnUrls__0: https://signup.${DOMINIO}/
  DataProtection__KeysPath: /keys
---
apiVersion: v1
kind: ConfigMap
metadata: {name: signups, namespace: ${NS}}
data:
  SignupApiBaseUrl: http://signupapi.${NS}.svc.cluster.local
  SignupApiPublicUrl: https://auth-api.${DOMINIO}
  PortalSessionBounce__PublicBaseUrl: https://signup.${DOMINIO}
  PortalSessionBounce__Portals__0__Name: BizCenterWeb
  PortalSessionBounce__Portals__0__SignOutUrl: https://app.${DOMINIO}/account/logout
  PortalSessionBounce__Portals__0__ProbeUrl: http://bizcenterweb.${NS}.svc.cluster.local/health
  PortalSessionBounce__Portals__1__Name: AdminWeb
  PortalSessionBounce__Portals__1__SignOutUrl: https://admin.${DOMINIO}/account/logout
  PortalSessionBounce__Portals__1__ProbeUrl: http://adminweb.${NS}.svc.cluster.local/health
  DataProtection__KeysPath: /keys
EOF
aplicar ~/mlm-k8s/$ENTORNO/configmaps.yaml
kubectl get configmap -n $NS
```

Hay tres cosas que dependen de que el portal de alta y los portales compartan dominio:
- El alta y los portales comparten el dominio registrable (`*.${DOMINIO}`). Eso permite que la cookie `SameSite=Strict` de los portales viaje en el rebote de cierre de sesión del alta.
- Si algún día el alta se sirve desde otro dominio, se pone `PortalSessionBounce__Enabled` en `false`.
- `ReceiptStorage__PublicBaseUrl` solo forma la URL que se guarda con cada recibo. El recibo se descarga a través de AdminAPI, que lo lee del volumen `recibos`.

---

## 13. Fase 11 — Despliegue de los servicios

**Qué se persigue:** poner en marcha los 11 servicios en el orden correcto, cada uno con su cuenta de servicio, su configuración y sus volúmenes, y comprobar que todos leyeron sus secretos y están sanos antes de abrir la entrada pública.

**Por qué así:**
- **El manifiesto no lleva ningún secreto:** solo dice qué imagen, qué cuenta de servicio, qué ConfigMaps y qué volúmenes. Se puede guardar, revisar y comparar sin cuidado especial.
- **Cada Deployment usa la cuenta de servicio de su nombre** (Fase 9), y con ella su rol. El servicio lee sus secretos al arrancar; si falta uno o no tiene permiso, el pod no arranca y el log dice cuál y por qué, en lugar de arrancar con un valor equivocado.
- **Contenedores endurecidos:** usuario no root, sistema de archivos de solo lectura salvo `/tmp` y los volúmenes, sin privilegios de Linux y sin el token de la API de Kubernetes.
- **Despliegue gradual** (una réplica nueva antes de quitar la vieja), **reparto por zonas** y un límite de interrupciones: un despliegue o un mantenimiento de nodos nunca deja un servicio sin réplicas.
- **Uno por uno y en orden:** SignupAPI siembra los roles y la cuenta raíz, y AdminAPI la empresa y las pasarelas. Si uno falla, el bucle se detiene ahí y no se arrastra el error.

**Alternativas descartadas:**
- **Helm o Kustomize desde el principio:** más potentes, pero otra herramienta que aprender para once manifiestos casi iguales. La función `gen` de abajo deja un archivo completo por servicio que se lee sin herramientas.
- **Montar los secretos como archivos o variables desde Secrets de Kubernetes:** obliga a copiar los secretos al clúster, que es justo lo que se evita.
- **Arrancar los 11 a la vez:** las semillas de SignupAPI y AdminAPI chocarían con los servicios que las necesitan.

### 13.1 Manifiestos de los servicios

Todos los Deployments tienen las mismas características:
- Arrancan con 1 réplica, que se sube en la Fase 14 (sección 16.1).
- Usan la cuenta de servicio con el nombre del servicio. No montan el token de la API de Kubernetes (`automountServiceAccountToken: false`); el token de IRSA lo inyecta EKS aparte, en su propio volumen, a todo pod cuya cuenta de servicio tenga la anotación del rol.
- Corren como usuario no root, con el sistema de archivos raíz de solo lectura salvo `/tmp`.
- La prueba de arranque espera hasta 10 minutos, porque el primer arranque aplica las semillas.
- La prueba de disponibilidad usa `/health`.
- La de vida comprueba el puerto, para que una caída de la base no reinicie todos los pods.

Se crea un archivo completo por servicio en `~/mlm-k8s/$ENTORNO/svc/`. Primero se define la función que escribe la parte común:

```
mkdir -p ~/mlm-k8s/$ENTORNO/svc
gen() {  # gen <servicio> <configmaps> <montajes> <volumenes>
cat > ~/mlm-k8s/$ENTORNO/svc/$1.yaml <<EOF
apiVersion: apps/v1
kind: Deployment
metadata: {name: $1, namespace: \${NS}, labels: {app: $1}}
spec:
  replicas: 1
  revisionHistoryLimit: 5
  selector: {matchLabels: {app: $1}}
  strategy: {type: RollingUpdate, rollingUpdate: {maxSurge: 1, maxUnavailable: 0}}
  template:
    metadata: {labels: {app: $1}}
    spec:
      serviceAccountName: $1
      automountServiceAccountToken: false
      securityContext: {runAsNonRoot: true, runAsUser: 1654, fsGroup: 1654,
                        seccompProfile: {type: RuntimeDefault}}
      topologySpreadConstraints:
        - {maxSkew: 1, topologyKey: topology.kubernetes.io/zone,
           whenUnsatisfiable: ScheduleAnyway, labelSelector: {matchLabels: {app: $1}}}
      containers:
        - name: app
          image: \${ECR}/mlmconqueror/$1:\${TAG}
          ports: [{name: http, containerPort: 8080}]
          envFrom:
$2
          resources: {requests: {cpu: 250m, memory: 512Mi}, limits: {memory: 1536Mi}}
          securityContext: {allowPrivilegeEscalation: false, readOnlyRootFilesystem: true,
                            capabilities: {drop: [ALL]}}
          startupProbe: {httpGet: {path: /health, port: http}, periodSeconds: 10,
                         failureThreshold: 60}
          readinessProbe: {httpGet: {path: /health, port: http}, periodSeconds: 10,
                           failureThreshold: 3}
          livenessProbe: {tcpSocket: {port: http}, periodSeconds: 20, failureThreshold: 3}
          volumeMounts:
            - {name: tmp, mountPath: /tmp}
$3
      volumes:
        - {name: tmp, emptyDir: {}}
$4
---
apiVersion: v1
kind: Service
metadata: {name: $1, namespace: \${NS}, labels: {app: $1}}
spec:
  selector: {app: $1}
  ports: [{name: http, port: 80, targetPort: http}]
---
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata: {name: $1, namespace: \${NS}}
spec:
  maxUnavailable: 1
  selector: {matchLabels: {app: $1}}
EOF
}
cm() { for c in "$@"; do echo "            - configMapRef: {name: $c}"; done; }
```

Fragmentos de montaje que se repiten. Solo hay volúmenes de datos: ningún secreto se monta como archivo.

```
M_KEYS='            - {name: llaves, mountPath: /keys}'
M_UP='            - {name: uploads, mountPath: /app/wwwroot/uploads}'
V_UP='        - {name: uploads, persistentVolumeClaim: {claimName: uploads}}'
M_REC='            - {name: recibos, mountPath: /data/payout-receipts}'
V_REC='        - {name: recibos, persistentVolumeClaim: {claimName: recibos}}'
nl=$'\n'
```

Generar los 11 archivos:

```
gen signupapi "$(cm comun signupapi)" "$M_KEYS" \
  "        - {name: llaves, persistentVolumeClaim: {claimName: llaves-signupapi}}"
gen adminapi "$(cm comun adminapi)" "$M_UP$nl$M_REC" "$V_UP$nl$V_REC"
gen billing "$(cm comun billing)" "$M_REC" "$V_REC"
gen bizcenter "$(cm comun bizcenter)" "$M_UP" "$V_UP"
gen commissionengine "$(cm comun)" "" ""
gen rankengine "$(cm comun rankengine)" \
  "            - {name: certificados, mountPath: /app/wwwroot/certificates}" \
  "        - {name: certificados, persistentVolumeClaim: {claimName: certificados}}"
gen ticketmanagementsystem "$(cm comun)" "" ""
gen sharedapicenter "$(cm comun)" "" ""
gen adminweb "$(cm comun adminweb)" "$M_KEYS" \
  "        - {name: llaves, persistentVolumeClaim: {claimName: llaves-adminweb}}"
gen bizcenterweb "$(cm comun bizcenterweb)" "$M_KEYS" \
  "        - {name: llaves, persistentVolumeClaim: {claimName: llaves-bizcenterweb}}"
gen signups "$(cm comun signups)" "$M_KEYS" \
  "        - {name: llaves, persistentVolumeClaim: {claimName: llaves-signups}}"
ls ~/mlm-k8s/$ENTORNO/svc       # 11 archivos
grep -l "serviceAccountName" ~/mlm-k8s/$ENTORNO/svc/*.yaml | wc -l    # 11
```

Con Firebase o sin él, las llamadas son las mismas: la credencial llega en el secreto `firebase`.

**Comprobar antes de aplicar.** Validación en el servidor, sin crear nada:

```
for f in ~/mlm-k8s/$ENTORNO/svc/*.yaml; do
  envsubst "$VARS_GUIA" < $f | kubectl apply --dry-run=server -f - > /dev/null \
    && echo "ok  $f" || echo "MAL $f"
done
```

Las 11 líneas tienen que decir `ok`.

### 13.2 Orden de arranque

El orden importa: SignupAPI siembra los roles, la cuenta raíz, los países y los productos, y AdminAPI siembra los datos de la empresa y las reglas de las pasarelas. Se aplica uno, se espera a que esté disponible y se sigue con el siguiente.

```
for s in signupapi adminapi billing bizcenter rankengine commissionengine \
         ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups; do
  echo "== $s"; aplicar ~/mlm-k8s/$ENTORNO/svc/$s.yaml
  kubectl rollout status deployment/$s -n $NS --timeout=12m || break
done
```

Si alguno no llega a disponible, el bucle se detiene. Se diagnostica con:

```
kubectl get pods -n $NS -l app=<servicio>
kubectl logs -n $NS deployment/<servicio> --tail=200
kubectl describe pod -n $NS -l app=<servicio>
```

Estas son las causas habituales:

| Causa | Síntoma en el log |
|---|---|
| El rol no tiene permiso sobre un secreto de la lista, o sobre la llave KMS | `[SecretosAws] La identidad de este servicio no tiene permiso sobre 'mlmconqueror/<entorno>/<secreto>'`. Se revisa la tabla y se vuelve a correr `crear-roles.sh` (sección 8.8) |
| El secreto no existe en el entorno | `[SecretosAws] El secreto 'mlmconqueror/<entorno>/<secreto>' no existe en ese entorno`. Se corre `crear-kms-y-secretos.sh` (sección 7.2) |
| El pod no tiene identidad de AWS | `[SecretosAws] Este servicio no tiene identidad de AWS`. Falta la cuenta de servicio, su anotación con el rol (sección 11.1) o `serviceAccountName` en el Deployment |
| El valor del secreto no es un JSON de pares clave-texto | `[SecretosAws] '<secreto>' no es un JSON de pares clave-texto`. Se vuelve a cargar con `cargar-secreto.sh`, que comprueba el formato |
| `SecretosAws__Entorno` vacío o mal escrito | `[SecretosAws] Entorno '...' no válido` (ConfigMap `comun`) |
| Falta una variable obligatoria | `is not configured` |
| No se puede leer el PFX | `DataProtection` (revisar `DataProtection__Certificate__Base64` y la contraseña en `dp-gateway`) |
| Falla la conexión a la base | `A network-related` (grupo de seguridad) o `certificate chain` (falta la CA de RDS en la imagen) |
| Falla Redis | `Cache:Mode=Required` sin Redis alcanzable |
| Escritura fuera de los volúmenes | `Read-only file system`: se monta un `emptyDir` en la ruta que aparece en el error |

**Comprobar:** `kubectl get pods -n $NS` muestra los 11 en `Running` y `1/1`, y cada servicio escribió sus secretos en el log:

```
for s in $SERVICIOS; do
  echo "== $s"; kubectl logs -n $NS deployment/$s | grep "\[SecretosAws\]"
done
```

Cada servicio muestra una línea `[SecretosAws] mlmconqueror/<entorno>/<secreto>: N clave(s).` por cada secreto de su fila (sección 8.1), con el entorno correcto. Signups no muestra ninguna, porque no lee secretos. Ninguna línea trae valores.

### 13.3 Comprobación interna de salud

```
kubectl run curl -n $NS --rm -i --restart=Never --image=curlimages/curl -- sh -c '
for s in signupapi adminapi billing bizcenter rankengine commissionengine \
  ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups; do
  printf "%-24s" $s; curl -s -o /dev/null -w "%{http_code}\n" http://$s/health; done'
```

Las 11 líneas tienen que mostrar 200.

---

## 14. Fase 12 — Balanceador interno: grupo de seguridad e Ingress

**Qué se persigue:** un único Application Load Balancer que reparte el tráfico entre los servicios por nombre de host, **sin IP pública**, y que solo acepta conexiones que vienen de CloudFront.

**Por qué así:**
- **ALB interno (`scheme: internal`):** el balanceador vive en las subredes privadas y su nombre DNS resuelve a IP privadas de la VPC. Desde internet no hay nada a lo que conectarse: no se puede saltar CloudFront ni su WAF yendo directo al balanceador, porque no existe un camino.
- **Grupo de seguridad propio, que solo admite la lista de prefijos administrada de CloudFront** (`com.amazonaws.global.cloudfront.origin-facing`) en el puerto 443. AWS mantiene esa lista al día; nadie tiene que copiar rangos de IP. Ni siquiera otro recurso de la VPC puede hablar con el ALB.
- **Solo HTTPS (443):** CloudFront habla con el ALB cifrado, con el certificado de ACM del dominio, y el ALB le dice al servicio `X-Forwarded-Proto: https`. El paso de HTTP a HTTPS lo hace CloudFront con el cliente; el ALB no necesita escuchar en el 80.
- **El ALB sigue haciendo lo que hace bien:** enrutar por nombre, comprobar la salud de cada pod, dar afinidad de sesión a los portales Blazor y bloquear con un 404 `/swagger`, `/hangfire`, `/api/v1/dev` y todo lo que no se publica.

**Alternativas descartadas:**
- **ALB público (con IP de internet) limitado a las IP de CloudFront y con una cabecera secreta** que CloudFront agrega y el ALB exige: el ALB seguiría teniendo IP pública, alcanzable por cualquiera que la encuentre. La cabecera es un secreto más que hay que guardar y rotar, y si se filtra, el WAF se puede saltar.
- **ALB público sin CloudFront, con el WAF en el ALB:** el ALB queda expuesto directamente, sin la red de CloudFront para absorber ataques de volumen.
- **Un ALB por servicio:** ocho balanceadores que pagar y vigilar para el mismo trabajo.

### 14.1 Grupo de seguridad del ALB: solo CloudFront

```
export CF_PL=$(aws ec2 describe-managed-prefix-lists \
  --filters Name=prefix-list-name,Values=com.amazonaws.global.cloudfront.origin-facing \
  --query 'PrefixLists[0].PrefixListId' --output text)
export SG_ALB=$(aws ec2 create-security-group --group-name mlm-$ENTORNO-alb \
  --description "ALB interno de mlm $ENTORNO: solo CloudFront" --vpc-id $VPC_ID \
  --query GroupId --output text)
aws ec2 authorize-security-group-ingress --group-id $SG_ALB --ip-permissions \
  "IpProtocol=tcp,FromPort=443,ToPort=443,PrefixListIds=[{PrefixListId=$CF_PL}]"
cat >> ~/mlm-$ENTORNO.env <<EOF
export CF_PL=$CF_PL
export SG_ALB=$SG_ALB
EOF
```

La lista de prefijos de CloudFront cuenta como muchas reglas dentro del grupo. Si el último comando responde `RulesPerSecurityGroupLimitExceeded`, se pide en Service Quotas (Amazon VPC, "Inbound or outbound rules per security group") un límite mayor y se repite.

**Comprobar:** `aws ec2 describe-security-groups --group-ids $SG_ALB --query 'SecurityGroups[0].IpPermissions'` muestra una sola regla: puerto 443 desde `$CF_PL`, sin ningún rango `0.0.0.0/0`.

### 14.2 Ingress

Un solo ALB (`group.name: mlmconqueror`) con tres Ingress:
- **Portales:** con afinidad de sesión, que Blazor Server necesita.
- **APIs públicas:** sin afinidad, y con `/swagger` y `/hangfire` bloqueados desde fuera.
- **Rutas parciales:** RankEngine e integraciones, que solo publican lo necesario.

Las rutas de webhooks de SharedAPICenter (`/api/v1/webhooks/*`) no se publican. El propio código las marca como bloqueante: no comprueban firma.

Los tres Ingress llevan el grupo de seguridad de la sección 14.1. Con `manage-backend-security-group-rules`, el controlador abre los pods solo al tráfico que viene del ALB.

```
cat > ~/mlm-k8s/$ENTORNO/ingress.yaml <<'EOF'
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: portales
  namespace: ${NS}
  annotations:
    alb.ingress.kubernetes.io/group.name: mlmconqueror
    alb.ingress.kubernetes.io/group.order: "10"
    alb.ingress.kubernetes.io/scheme: internal
    alb.ingress.kubernetes.io/target-type: ip
    alb.ingress.kubernetes.io/listen-ports: '[{"HTTPS":443}]'
    alb.ingress.kubernetes.io/certificate-arn: ${CERT_ARN}
    alb.ingress.kubernetes.io/ssl-policy: ELBSecurityPolicy-TLS13-1-2-2021-06
    alb.ingress.kubernetes.io/security-groups: ${SG_ALB}
    alb.ingress.kubernetes.io/manage-backend-security-group-rules: "true"
    alb.ingress.kubernetes.io/healthcheck-path: /health
    alb.ingress.kubernetes.io/load-balancer-attributes: >-
      idle_timeout.timeout_seconds=120,routing.http.drop_invalid_header_fields.enabled=true
    alb.ingress.kubernetes.io/target-group-attributes: >-
      stickiness.enabled=true,stickiness.type=lb_cookie,stickiness.lb_cookie.duration_seconds=86400
spec:
  ingressClassName: alb
  rules:
    - host: admin.${DOMINIO}
      http: {paths: [{path: /, pathType: Prefix,
             backend: {service: {name: adminweb, port: {name: http}}}}]}
    - host: app.${DOMINIO}
      http: {paths: [{path: /, pathType: Prefix,
             backend: {service: {name: bizcenterweb, port: {name: http}}}}]}
    - host: signup.${DOMINIO}
      http: {paths: [{path: /, pathType: Prefix,
             backend: {service: {name: signups, port: {name: http}}}}]}
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: apis
  namespace: ${NS}
  annotations:
    alb.ingress.kubernetes.io/group.name: mlmconqueror
    alb.ingress.kubernetes.io/group.order: "20"
    alb.ingress.kubernetes.io/scheme: internal
    alb.ingress.kubernetes.io/target-type: ip
    alb.ingress.kubernetes.io/listen-ports: '[{"HTTPS":443}]'
    alb.ingress.kubernetes.io/certificate-arn: ${CERT_ARN}
    alb.ingress.kubernetes.io/ssl-policy: ELBSecurityPolicy-TLS13-1-2-2021-06
    alb.ingress.kubernetes.io/security-groups: ${SG_ALB}
    alb.ingress.kubernetes.io/manage-backend-security-group-rules: "true"
    alb.ingress.kubernetes.io/healthcheck-path: /health
    alb.ingress.kubernetes.io/actions.bloqueo: >-
      {"type":"fixed-response","fixedResponseConfig":{"contentType":"text/plain",
      "statusCode":"404","messageBody":"Not found"}}
spec:
  ingressClassName: alb
  rules:
    - host: auth-api.${DOMINIO}
      http:
        paths:
          - {path: /swagger, pathType: Prefix,
             backend: {service: {name: bloqueo, port: {name: use-annotation}}}}
          - {path: /hangfire, pathType: Prefix,
             backend: {service: {name: bloqueo, port: {name: use-annotation}}}}
          - {path: /api/v1/dev, pathType: Prefix,
             backend: {service: {name: bloqueo, port: {name: use-annotation}}}}
          - {path: /, pathType: Prefix,
             backend: {service: {name: signupapi, port: {name: http}}}}
    - host: admin-api.${DOMINIO}
      http:
        paths:
          - {path: /swagger, pathType: Prefix,
             backend: {service: {name: bloqueo, port: {name: use-annotation}}}}
          - {path: /, pathType: Prefix,
             backend: {service: {name: adminapi, port: {name: http}}}}
    - host: api.${DOMINIO}
      http:
        paths:
          - {path: /swagger, pathType: Prefix,
             backend: {service: {name: bloqueo, port: {name: use-annotation}}}}
          - {path: /hangfire, pathType: Prefix,
             backend: {service: {name: bloqueo, port: {name: use-annotation}}}}
          - {path: /, pathType: Prefix,
             backend: {service: {name: bizcenter, port: {name: http}}}}
    - host: rank.${DOMINIO}
      http: {paths: [{path: /certificates, pathType: Prefix,
             backend: {service: {name: rankengine, port: {name: http}}}}]}
    - host: integrations.${DOMINIO}
      http: {paths: [{path: /api/v1/external, pathType: Prefix,
             backend: {service: {name: sharedapicenter, port: {name: http}}}}]}
EOF
aplicar ~/mlm-k8s/$ENTORNO/ingress.yaml
kubectl get ingress -n $NS -w      # esperar a que ADDRESS muestre el nombre del ALB (2-4 min)
```

En RankEngine e integraciones, el ALB solo comprueba la salud del destino en `/health`, que existe en los dos.

### 14.3 Datos del ALB y comprobación de que es interno

```
export ALB_DNS=$(kubectl get ingress portales -n $NS \
  -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
export ALB_ARN=$(aws elbv2 describe-load-balancers \
  --query "LoadBalancers[?DNSName=='$ALB_DNS'].LoadBalancerArn" --output text)
cat >> ~/mlm-$ENTORNO.env <<EOF
export ALB_DNS=$ALB_DNS
export ALB_ARN=$ALB_ARN
EOF
aws elbv2 describe-load-balancers --load-balancer-arns $ALB_ARN \
  --query 'LoadBalancers[0].[Scheme,SecurityGroups]'
dig +short $ALB_DNS
```

**Comprobar:** el esquema es `internal`, entre los grupos de seguridad está `$SG_ALB`, y `dig` devuelve solo IP privadas de la VPC (`10.20.x.x`).

---

## 15. Fase 13 — CloudFront, WAF y DNS

**Qué se persigue:** abrir los ocho nombres públicos a internet a través de CloudFront, con el WAF delante, de modo que CloudFront sea la única puerta y llegue al ALB interno por una conexión privada.

**Por qué así:**
- **VPC origin:** CloudFront crea sus propias interfaces de red dentro de la VPC y desde ahí habla con el ALB interno. El balanceador no necesita IP pública ni ningún secreto compartido con CloudFront.
- **HTTPS también entre CloudFront y el ALB**, con el certificado del dominio: el tráfico va cifrado de punta a punta, y el servicio sabe que el cliente llegó por HTTPS.
- **WAF en CloudFront (alcance `CLOUDFRONT`):** filtra en el borde, antes de que la petición entre a la VPC, y ve la IP real del cliente. El límite de 2.000 peticiones por IP cada 5 minutos se aplica a cada cliente. Las reglas administradas de AWS (comunes, entradas conocidas como maliciosas e inyección SQL) se actualizan solas.
- **AWS Shield Standard** viene incluido con CloudFront y absorbe los ataques de volumen en la red de AWS, lejos del clúster.
- **Caché apagada (`CachingDisabled`) y todo reenviado al origen (`AllViewer`):** todo el contenido es dinámico y depende de quién lo pide (portales con sesión, APIs con token). Así nunca se sirve a un cliente la página de otro. CloudFront reenvía el `Host` (el ALB enruta por nombre), las cookies (la sesión y la afinidad del ALB), la cadena de consulta y las cabeceras. Los WebSockets de Blazor Server funcionan sin configuración extra.
- **El DNS apunta a CloudFront con registros alias:** no hay IP que mantener y Route 53 no cobra las consultas de alias.

**Alternativas descartadas:**
- **ALB público restringido por IP y cabecera secreta:** ver la Fase 12. Más superficie y un secreto más.
- **WAF en el ALB:** detrás de CloudFront, el WAF del ALB vería como origen las IP de CloudFront, así que el límite por IP bloquearía a CloudFront entero (a todos los clientes a la vez) o no bloquearía a nadie. Además filtra más tarde, cuando el tráfico ya entró a la VPC.
- **Activar la caché para el contenido dinámico:** riesgo de servir la respuesta de un usuario a otro y de servir datos viejos. La ganancia sería pequeña: los archivos estáticos de los portales pesan poco frente al tráfico de las sesiones.
- **Otro CDN (Cloudflare) delante de EKS:** sirve, pero no puede entrar a la VPC por una conexión privada, así que el ALB tendría que ser público. Cloudflare se usa en el despliegue con Docker, donde no hay VPC (`docs/deployment/despliegue-docker.md`).

### 15.1 AWS WAF con alcance de CloudFront

Las reglas de un WAF de CloudFront se crean siempre en `us-east-1`:

```
cat > /tmp/waf-rules.json <<'EOF'
[{"Name":"common","Priority":1,"OverrideAction":{"None":{}},
  "Statement":{"ManagedRuleGroupStatement":{
    "VendorName":"AWS","Name":"AWSManagedRulesCommonRuleSet"}},
  "VisibilityConfig":{"SampledRequestsEnabled":true,
    "CloudWatchMetricsEnabled":true,"MetricName":"common"}},
 {"Name":"badinputs","Priority":2,"OverrideAction":{"None":{}},
  "Statement":{"ManagedRuleGroupStatement":{
    "VendorName":"AWS","Name":"AWSManagedRulesKnownBadInputsRuleSet"}},
  "VisibilityConfig":{"SampledRequestsEnabled":true,
    "CloudWatchMetricsEnabled":true,"MetricName":"badinputs"}},
 {"Name":"sqli","Priority":3,"OverrideAction":{"None":{}},
  "Statement":{"ManagedRuleGroupStatement":{
    "VendorName":"AWS","Name":"AWSManagedRulesSQLiRuleSet"}},
  "VisibilityConfig":{"SampledRequestsEnabled":true,
    "CloudWatchMetricsEnabled":true,"MetricName":"sqli"}},
 {"Name":"ratelimit","Priority":4,"Action":{"Block":{}},
  "Statement":{"RateBasedStatement":{"Limit":2000,"AggregateKeyType":"IP"}},
  "VisibilityConfig":{"SampledRequestsEnabled":true,
    "CloudWatchMetricsEnabled":true,"MetricName":"ratelimit"}}]
EOF
export WAF_ARN=$(aws wafv2 create-web-acl --region us-east-1 --name mlm-$ENTORNO \
  --scope CLOUDFRONT --default-action Allow={} --rules file:///tmp/waf-rules.json \
  --visibility-config \
  SampledRequestsEnabled=true,CloudWatchMetricsEnabled=true,MetricName=mlm-$ENTORNO \
  --query Summary.ARN --output text)
echo "export WAF_ARN=$WAF_ARN" >> ~/mlm-$ENTORNO.env
```

### 15.2 VPC origin hacia el ALB interno

CloudFront llega al ALB por HTTPS. El ALB tiene que estar en subredes privadas de una VPC con Internet Gateway, que es como la crea la Fase 3.

```
cat > /tmp/vpc-origin.json <<EOF
{"Name":"mlm-$ENTORNO-alb","Arn":"$ALB_ARN","HTTPPort":80,"HTTPSPort":443,
 "OriginProtocolPolicy":"https-only",
 "OriginSslProtocols":{"Quantity":1,"Items":["TLSv1.2"]}}
EOF
export VPCO_ID=$(aws cloudfront create-vpc-origin \
  --vpc-origin-endpoint-config file:///tmp/vpc-origin.json \
  --query VpcOrigin.Id --output text)
echo "export VPCO_ID=$VPCO_ID" >> ~/mlm-$ENTORNO.env
until [ "$(aws cloudfront get-vpc-origin --id $VPCO_ID --query VpcOrigin.Status \
  --output text)" = Deployed ]; do echo "esperando el VPC origin..."; sleep 30; done
```

Tarda hasta 15 minutos. El bucle termina cuando el estado es `Deployed`.

### 15.3 Distribución

Las dos políticas administradas se buscan por nombre (`Managed-CachingDisabled` y `Managed-AllViewer`):

```
CACHE_OFF=$(aws cloudfront list-cache-policies --type managed --output json \
  | jq -r '.CachePolicyList.Items[].CachePolicy
           | select(.CachePolicyConfig.Name == "Managed-CachingDisabled") | .Id')
TODO_VIEWER=$(aws cloudfront list-origin-request-policies --type managed --output json \
  | jq -r '.OriginRequestPolicyList.Items[].OriginRequestPolicy
           | select(.OriginRequestPolicyConfig.Name == "Managed-AllViewer") | .Id')
echo $CACHE_OFF $TODO_VIEWER   # dos identificadores, ninguno vacío
```

Una sola distribución sirve los ocho nombres. El comportamiento por defecto admite todos los métodos, no guarda nada y pasa de HTTP a HTTPS:

```
NOMBRES=$(for h in admin app signup auth-api admin-api api rank integrations; do
  echo "$h.$DOMINIO"; done | jq -R . | jq -s -c .)
jq -n --arg ref "mlm-$ENTORNO-$(date +%s)" --arg com "mlmconqueror $ENTORNO" \
  --argjson nombres "$NOMBRES" --arg alb "$ALB_DNS" --arg vpco "$VPCO_ID" \
  --arg cache "$CACHE_OFF" --arg orp "$TODO_VIEWER" --arg cert "$CERT_ARN" --arg waf "$WAF_ARN" '{
  CallerReference: $ref, Comment: $com, Enabled: true,
  Aliases: {Quantity: ($nombres | length), Items: $nombres},
  Origins: {Quantity: 1, Items: [{Id: "alb", DomainName: $alb, OriginPath: "",
    CustomHeaders: {Quantity: 0}, VpcOriginConfig: {VpcOriginId: $vpco}}]},
  DefaultCacheBehavior: {TargetOriginId: "alb", ViewerProtocolPolicy: "redirect-to-https",
    AllowedMethods: {Quantity: 7, Items: ["GET","HEAD","OPTIONS","PUT","POST","PATCH","DELETE"],
      CachedMethods: {Quantity: 2, Items: ["GET","HEAD"]}},
    Compress: false, CachePolicyId: $cache, OriginRequestPolicyId: $orp},
  ViewerCertificate: {ACMCertificateArn: $cert, SSLSupportMethod: "sni-only",
    MinimumProtocolVersion: "TLSv1.2_2021"},
  WebACLId: $waf, HttpVersion: "http2and3", IsIPV6Enabled: true,
  PriceClass: "PriceClass_All" }' > /tmp/cf.json
aws cloudfront create-distribution --distribution-config file:///tmp/cf.json \
  --query 'Distribution.[Id,DomainName]' --output text > /tmp/cf-id
export CF_ID=$(cut -f1 /tmp/cf-id) CF_DNS=$(cut -f2 /tmp/cf-id)
cat >> ~/mlm-$ENTORNO.env <<EOF
export CF_ID=$CF_ID
export CF_DNS=$CF_DNS
EOF
aws cloudfront wait distribution-deployed --id $CF_ID      # 5-15 min
```

`PriceClass_All` sirve desde todas las ubicaciones de CloudFront, porque los miembros están en varios continentes. Si todos estuvieran en América y Europa, `PriceClass_100` cuesta menos.

**Comprobar:** `aws cloudfront get-distribution --id $CF_ID --query 'Distribution.[Status,DistributionConfig.WebACLId]'` muestra `Deployed` y el ARN de `$WAF_ARN`.

### 15.4 Registros DNS hacia CloudFront

Cada nombre es un alias (`A` y `AAAA`) a la distribución. `Z2FDTNDATAQYW2` es la zona fija que AWS usa para todos los alias de CloudFront:

```
CAMBIOS=""
for h in admin app signup auth-api admin-api api rank integrations; do
  for t in A AAAA; do
    CAMBIOS="$CAMBIOS{\"Action\":\"UPSERT\",\"ResourceRecordSet\":{\"Name\":\"$h.$DOMINIO\",
    \"Type\":\"$t\",\"AliasTarget\":{\"HostedZoneId\":\"Z2FDTNDATAQYW2\",\"DNSName\":\"$CF_DNS\",
    \"EvaluateTargetHealth\":false}}},"
  done
done
echo "{\"Changes\":[${CAMBIOS%,}]}" > /tmp/dns.json
aws route53 change-resource-record-sets --hosted-zone-id $ZONA_ID --change-batch file:///tmp/dns.json
rm -f /tmp/dns.json /tmp/cf.json /tmp/cf-id /tmp/vpc-origin.json /tmp/waf-rules.json
```

### 15.5 Comprobaciones del borde

A los pocos minutos:

```
for h in admin app signup auth-api admin-api api; do
  printf "%-12s" $h; curl -s -o /dev/null -w "%{http_code}\n" https://$h.$DOMINIO/health; done
curl -s https://auth-api.$DOMINIO/.well-known/jwks.json | jq '.keys[0].kid'
curl -s -o /dev/null -w "%{http_code}\n" https://auth-api.$DOMINIO/swagger     # 404
curl -s -o /dev/null -w "%{http_code}\n" http://admin.$DOMINIO/                # 301
curl -sI https://admin.$DOMINIO/health | grep -i '^x-cache'      # Miss from cloudfront
curl -sk --max-time 10 -o /dev/null -w "%{http_code}\n" https://$ALB_DNS/health  # 000
```

- Los seis primeros dan 200, el JWKS devuelve un `kid`, Swagger da 404 y HTTP da 301 a HTTPS.
- `X-Cache` dice `Miss from cloudfront` (o `RefreshHit`): la respuesta pasó por CloudFront y no salió de la caché.
- **El ALB no responde desde internet:** la última línea da `000` después de 10 segundos, porque su nombre resuelve a IP privadas. Si responde 200, el ALB es público y hay que revisar `scheme: internal` en los tres Ingress.

**La IP que ve el servicio es la del cliente.** Desde un navegador, en una red cuya IP pública se conoce (la de la oficina, que muestra `curl -s https://checkip.amazonaws.com`), se abre `https://signup.${DOMINIO}`, se empieza un alta de embajador con datos de prueba y se avanza hasta el primer paso, sin pagar. SignupAPI guarda la IP de cada intento en `SignupRiskFingerprints`. Se lee desde el pod de `sqlcmd`: se repiten los comandos de la sección 10.2 (el pod, `MASTER_PASS` y la función `sq`) y después:

```
sq -d MLMConquerorGlobalEditionDb -h -1 -Q \
  "SELECT TOP 3 IpAddress, CreationDate FROM SignupRiskFingerprints ORDER BY Id DESC;"
kubectl delete pod -n $NS sqlcmd
unset MASTER_PASS APP_PASS
```

La IP de la primera fila tiene que ser la de la oficina. Si aparece una IP de la VPC (`10.20.x.x`) o una de CloudFront, `ForwardedHeaders__ForwardLimit` o `ForwardedHeaders__KnownNetwork` del ConfigMap `comun` están mal. En `prod` se hace antes de abrir al público, y el alta de prueba se deja sin completar.

### 15.6 Prueba funcional en el navegador

1. `https://signup.${DOMINIO}`: carga el alta, y en las herramientas de desarrollo las llamadas van a `auth-api` (Fase 0, cambio 4).
2. `https://admin.${DOMINIO}`: iniciar sesión con la cuenta raíz y cambiar su contraseña.
3. En Admin, S3 Config: poner el bucket `${BUCKET}` y la región `${AWS_REGION}`, **sin llaves de acceso** (usa la identidad del pod).
4. En Admin, pasarelas de pago: cargar las credenciales de PayQuicker, i-Payout y Volet de este entorno. Quedan cifradas con el anillo del PFX.
5. Pedir la recuperación de contraseña de un usuario de prueba con buzón real: el correo llega desde `${SES_FROM}`.
6. Con la sesión abierta en `admin`, ejecutar `kubectl rollout restart deployment/adminweb -n $NS`. Al terminar, la sesión sigue abierta (Fase 0, cambio 3).
7. Subir un adjunto a un ticket desde el portal de miembros y abrirlo desde administración (volumen `uploads`).
8. Revisar que los portales no muestran el aviso de licencia de Syncfusion.

---

## 16. Fase 14 — Operación: réplicas, paneles, alarmas, secretos y copias

**Qué se persigue:** dejar el sistema listo para el día a día: dos réplicas de cada servicio, avisos cuando algo falla, un procedimiento corto para cambiar un secreto y copias que se prueban.

**Por qué así:**
- **Dos réplicas repartidas por zona:** un pod que se cae, un nodo que se reemplaza o una zona de AWS con problemas no tumban ningún servicio.
- **Paneles de Hangfire por túnel local:** se revisan sin publicarlos; quien no tiene acceso al clúster no los ve.
- **Alarmas por correo** sobre lo que de verdad indica un problema: CPU y disco de la base, memoria de Redis, errores 5xx en el ALB y en CloudFront, y destinos caídos.
- **Cambiar un secreto es cargarlo y reiniciar:** el servicio lo lee al arrancar, así que el reinicio gradual pone el valor nuevo réplica a réplica, sin cortar el servicio. Quién lo cambió y cuándo queda en CloudTrail, y la versión anterior en Secrets Manager.
- **Copias automáticas y una prueba de restauración al mes:** una copia que nunca se restauró no se sabe si sirve.

**Alternativas descartadas:**
- **Recarga de secretos en caliente:** obligaría a que las conexiones de SQL y Redis y las llaves ya cargadas supieran recargarse. El reinicio gradual da el mismo resultado sin ese riesgo.
- **Rotación automática con Lambda desde el principio:** se puede agregar después sin cambiar los servicios; hoy los secretos se cambian pocas veces y a mano.
- **Autoescalado desde el primer día:** hasta tener datos de carga real, dos réplicas fijas son más fáciles de razonar.

### 16.1 Subir a 2 réplicas

Se hace después de la prueba funcional:

```
for s in $SERVICIOS; do kubectl scale deployment/$s -n $NS --replicas=2; done
kubectl get pods -n $NS -o wide     # 22 pods, repartidos por zona
```

Para que el número de réplicas no vuelva a 1 en el siguiente despliegue, se cambia `replicas: 1` por `replicas: 2` en los 11 archivos de `~/mlm-k8s/$ENTORNO/svc/`.

### 16.2 Paneles de Hangfire

Ningún panel se publica. Se abren con un túnel local:

```
kubectl port-forward -n $NS deployment/billing 9001:8080
# navegador: http://localhost:9001/hangfire
```

En cada panel se revisa que en Servers haya un servidor por réplica, que escucha su cola (`billing*`, `bizcenter`, `signups`, `commissions`, `rank`, `tickets`), y que los trabajos recurrentes tengan próxima ejecución. Las horas de los trabajos recurrentes están en UTC.

### 16.3 Alarmas

```
export SNS_ARN=$(aws sns create-topic --name mlm-$ENTORNO-alarmas --query TopicArn --output text)
read -p "Correo de guardia: " GUARDIA
aws sns subscribe --topic-arn $SNS_ARN --protocol email --notification-endpoint "$GUARDIA"
alarma() { aws cloudwatch put-metric-alarm --alarm-name "$1" --namespace "$2" \
  --metric-name "$3" --dimensions "$4" --statistic "$5" --period 300 \
  --evaluation-periods 2 --threshold "$6" --comparison-operator "$7" \
  --alarm-actions $SNS_ARN --ok-actions $SNS_ARN; }
alarma rds-cpu AWS/RDS CPUUtilization Name=DBInstanceIdentifier,Value=mlm-$ENTORNO-sql \
  Average 80 GreaterThanThreshold
alarma rds-disco AWS/RDS FreeStorageSpace Name=DBInstanceIdentifier,Value=mlm-$ENTORNO-sql \
  Minimum 21474836480 LessThanThreshold
alarma redis-memoria AWS/ElastiCache DatabaseMemoryUsagePercentage \
  Name=ReplicationGroupId,Value=mlm-$ENTORNO-redis Average 80 GreaterThanThreshold
ALB_ID=$(echo $ALB_ARN | sed 's#.*:loadbalancer/##')
alarma alb-5xx AWS/ApplicationELB HTTPCode_Target_5XX_Count Name=LoadBalancer,Value=$ALB_ID \
  Sum 50 GreaterThanThreshold
alarma alb-destinos-caidos AWS/ApplicationELB UnHealthyHostCount Name=LoadBalancer,Value=$ALB_ID \
  Maximum 0 GreaterThanThreshold
aws cloudwatch put-metric-alarm --region us-east-1 --alarm-name cloudfront-5xx \
  --namespace AWS/CloudFront --metric-name 5xxErrorRate \
  --dimensions Name=DistributionId,Value=$CF_ID Name=Region,Value=Global \
  --statistic Average --period 300 --evaluation-periods 2 --threshold 5 \
  --comparison-operator GreaterThanThreshold --alarm-actions $SNS_ARN --ok-actions $SNS_ARN
```

Hay que confirmar la suscripción desde el correo que manda SNS. Las métricas de CloudFront viven en `us-east-1`; como el entorno está en esa región, el mismo tema de SNS sirve para todas.

### 16.4 Cambiar un secreto

Desde una carpeta temporal como la de la sección 8.2, que se borra al final:

1. Partir del valor actual, porque `cargar-secreto.sh` reemplaza el secreto entero. Por ejemplo, para cambiar solo `Entrax__Secret` en `signupapi`:

```
umask 077; mkdir -p ~/mlm-secretos && cd ~/mlm-secretos; set +o history
aws secretsmanager get-secret-value --secret-id mlmconqueror/$ENTORNO/signupapi \
  --query SecretString --output text > actual.json
read -s -p "Valor nuevo: " V; echo
jq --arg v "$V" '.Entrax__Secret = $v' actual.json > nuevo.json
```

2. Cargarlo. El script lista los servicios que lo leen:

```
bash $REPO/deploy/aws/cargar-secreto.sh $ENTORNO signupapi nuevo.json
```

3. Reiniciar cada servicio de esa lista, en minúsculas. El reinicio es gradual: las réplicas nuevas arrancan con el valor nuevo antes de que se apaguen las viejas.

```
kubectl rollout restart deployment/signupapi -n $NS
kubectl rollout status deployment/signupapi -n $NS --timeout=12m
kubectl logs -n $NS deployment/signupapi | grep "\[SecretosAws\]"
```

4. Borrar la carpeta: `cd ~ && shred -u ~/mlm-secretos/*; rm -rf ~/mlm-secretos; unset V; set -o history`.

Si el pod nuevo no arranca, el log dice qué secreto falla y por qué (tabla de la sección 13.2), y las réplicas viejas siguen atendiendo. Para volver al valor anterior se carga la versión previa (`aws secretsmanager get-secret-value --version-stage AWSPREVIOUS`) con el mismo procedimiento.

La llave JWT tiene su propio procedimiento en `docs/deployment/rotacion-llaves-jwt.md`:
- Se cambian juntas la privada (`jwt-firma`) y la pública (`JWT_PUBLICA` en el ConfigMap `comun`).
- Se reinician primero signupapi y adminapi, y después el resto.
- Todas las sesiones se cierran. Las apps MAUI descargan la nueva llave del JWKS solas.

### 16.5 Copias de seguridad

- **RDS:** copias automáticas de 14 días, y además una instantánea manual antes de cada despliegue que traiga migraciones: `aws rds create-db-snapshot --db-instance-identifier mlm-$ENTORNO-sql --db-snapshot-identifier mlm-$ENTORNO-sql-antes-$TAG`.
- **EFS:** la política de copia activada en la Fase 5 la incluye en el plan diario por defecto de AWS Backup.
- **Secretos:** Secrets Manager guarda las versiones anteriores. El PFX y su contraseña además están en el gestor de contraseñas (Fase 6).
- **Prueba de restauración:** una vez al mes, restaurar la última instantánea de RDS en una instancia temporal y borrarla después.

---

## 17. Fase 15 — Encender lo que quedó apagado

**Qué se persigue:** activar, una por una, las funciones que el primer despliegue dejó apagadas a propósito: el pago en efectivo con Entrax, los SMS de Twilio y el 2FA obligatorio.

**Por qué así:**
- **Una a la vez y con su comprobación:** si algo falla, se sabe qué lo causó.
- **Los secretos ya existen, vacíos, y los servicios ya los leen:** encender una función es cargar el valor con `cargar-secreto.sh`, cambiar el interruptor en el ConfigMap y reiniciar. No se toca ningún permiso ni ningún manifiesto de Deployment.

**Alternativas descartadas:**
- **Encender todo en el primer despliegue:** si algo falla el primer día, hay demasiadas causas posibles.
- **Crear los secretos solo cuando hacen falta:** habría que volver a correr `crear-roles.sh` y los servicios fallarían al arrancar mientras tanto. Vacíos desde el principio, el arranque nunca depende de ellos.

Cada punto se hace por separado y se comprueba antes del siguiente.

### 17.1 Entrax

1. Pedir a Entrax el `KeyId` y el `Secret` de este entorno, y sus IP de salida.
2. Ponerlos en `mlmconqueror/$ENTORNO/signupapi` (`Entrax__KeyId` y `Entrax__Secret`), partiendo del valor actual como en la sección 16.4.
3. En el ConfigMap `signupapi`, poner `Entrax__Enabled: "true"` y aplicarlo con `aplicar ~/mlm-k8s/$ENTORNO/configmaps.yaml`.
4. Reiniciar signupapi.
5. Darle a Entrax la URL `https://auth-api.${DOMINIO}/api/v1/signups/entrax/callback`.
6. **Comprobar:** un POST sin firma a esa URL responde 401, y el trabajo `entrax-signup-sweep` aparece en el panel de SignupAPI.

### 17.2 SMS con Twilio y 2FA obligatorio

Se sigue `docs/deployment/verificacion-2fa.md`, en ese orden. Los pasos de EKS son estos:

1. Cargar `mlmconqueror/$ENTORNO/twilio` con `Notifications__Sms__Twilio__AccountSid` y `Notifications__Sms__Twilio__AuthToken`:

```
umask 077; mkdir -p ~/mlm-secretos && cd ~/mlm-secretos; set +o history
read -p "Twilio AccountSid: " SID; read -s -p "Twilio AuthToken: " TOK; echo
jq -n --arg s "$SID" --arg t "$TOK" '{"Notifications__Sms__Twilio__AccountSid": $s,
  "Notifications__Sms__Twilio__AuthToken": $t}' > twilio.json
bash $REPO/deploy/aws/cargar-secreto.sh $ENTORNO twilio twilio.json
cd ~ && shred -u ~/mlm-secretos/*; rm -rf ~/mlm-secretos; unset SID TOK; set -o history
```

   El script lista AdminAPI, Billing, BizCenter, RankEngine y SignupAPI: son los que lo leen. No hay que cambiar ningún permiso.
2. En el ConfigMap `comun`, poner `Notifications__Sms__Provider: Twilio` y `Notifications__Sms__Twilio__FromNumber`, y aplicarlo.
3. Reiniciar los cinco servicios de la lista del paso 1.
4. Antes de exigir el 2FA, purgar o cambiar las cuentas SuperAdmin cuyo correo sea de un buzón público. Si no, el código del 2FA llega a un buzón que puede leer cualquiera.
5. Agregar la misma lista `Auth__TwoFactor__MandatoryRoles__0`, `__1`... a los ConfigMaps de signupapi, adminweb y bizcenterweb. Tiene que ser idéntica en los tres.
6. Reiniciar los tres servicios.

### 17.3 Pago con tarjeta

`Signup__CardEnabled`, `Membership__UpgradeEnabled` y los dos `CardEnabled` de Billing se quedan en `false` hasta que exista el cobro real con 3DS de Spreedly. Hoy el código es un simulador, y encenderlo activa membresías sin cobrar.

---

## 18. Fase 16 — Despliegue continuo con GitHub Actions

**Qué se persigue:** que los despliegues siguientes salgan de GitHub con un clic, siempre desde `master`, siempre con la aprobación de Nick y sin guardar ninguna credencial de AWS en GitHub.

**Por qué así:**
- **OIDC entre GitHub y AWS:** en cada ejecución, GitHub presenta un token firmado que dice de qué repositorio y de qué entorno viene, y AWS le da credenciales de una hora del rol `MLM-GitHubDeploy`. No hay llave de acceso que se pueda filtrar desde los ajustes del repositorio.
- **El rol solo puede** subir imágenes a los repositorios de ECR del proyecto y editar los recursos de un solo namespace del clúster. **No puede leer ningún secreto de Secrets Manager:** los secretos los leen los servicios con sus propios roles.
- **Entorno de GitHub con revisor obligatorio:** ningún despliegue sale sin que Nick lo apruebe.
- **Las migraciones siguen siendo a mano** y con instantánea previa: un cambio de esquema es la única parte que no se deshace con `rollout undo`.

**Alternativas descartadas:**
- **Llaves de acceso de un usuario de IAM guardadas como secretos de GitHub:** no caducan, y quien tenga acceso de administración al repositorio las puede usar fuera de GitHub.
- **Desplegar con cada `push` a `master`:** sin una persona que decida cuándo sale a producción.
- **Que el flujo migre la base:** un error de migración a medio despliegue deja la base y los servicios en versiones que no casan.

### 18.1 Confianza entre GitHub y AWS (OIDC)

En `staging`, en la condición `sub` se cambia `environment:production` por `environment:staging`.

```
aws iam create-open-id-connect-provider --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com
cat > /tmp/gh-trust.json <<EOF
{"Version":"2012-10-17","Statement":[{"Effect":"Allow",
 "Principal":{"Federated":"arn:aws:iam::$CUENTA:oidc-provider/token.actions.githubusercontent.com"},
 "Action":"sts:AssumeRoleWithWebIdentity","Condition":{
  "StringEquals":{"token.actions.githubusercontent.com:aud":"sts.amazonaws.com",
   "token.actions.githubusercontent.com:sub":
   "repo:NickSagaracorp/MLMConquerorGlobalMicroServices:environment:production"}}}]}
EOF
aws iam create-role --role-name MLM-GitHubDeploy --assume-role-policy-document file:///tmp/gh-trust.json
cat > /tmp/gh-policy.json <<EOF
{"Version":"2012-10-17","Statement":[
 {"Effect":"Allow","Action":"ecr:GetAuthorizationToken","Resource":"*"},
 {"Effect":"Allow","Action":["ecr:BatchCheckLayerAvailability","ecr:InitiateLayerUpload",
  "ecr:UploadLayerPart","ecr:CompleteLayerUpload","ecr:PutImage","ecr:BatchGetImage",
  "ecr:DescribeImages"],"Resource":"arn:aws:ecr:$AWS_REGION:$CUENTA:repository/mlmconqueror/*"},
 {"Effect":"Allow","Action":["kms:GenerateDataKey","kms:Decrypt"],"Resource":"$KMS_ARN"},
 {"Effect":"Allow","Action":"eks:DescribeCluster",
  "Resource":"arn:aws:eks:$AWS_REGION:$CUENTA:cluster/$CLUSTER"}]}
EOF
aws iam put-role-policy --role-name MLM-GitHubDeploy --policy-name deploy \
  --policy-document file:///tmp/gh-policy.json
aws eks create-access-entry --cluster-name $CLUSTER \
  --principal-arn arn:aws:iam::$CUENTA:role/MLM-GitHubDeploy
aws eks associate-access-policy --cluster-name $CLUSTER \
  --principal-arn arn:aws:iam::$CUENTA:role/MLM-GitHubDeploy \
  --policy-arn arn:aws:eks::aws:cluster-access-policy/AmazonEKSEditPolicy \
  --access-scope type=namespace,namespaces=$NS
```

### 18.2 Entorno de GitHub

En GitHub, en Settings, Environments, se crea `production`:
- **Required reviewers:** Nick. Ningún despliegue sale sin su aprobación.
- **Deployment branches:** solo `master`.
- **Variables del entorno:** `AWS_REGION`, `AWS_ACCOUNT_ID` y `EKS_CLUSTER` (`mlmconqueror-prod`).

Para `staging` se crea además el entorno `staging`, con las variables de la cuenta de desarrollo y `EKS_CLUSTER` igual a `mlmconqueror-staging`, y el flujo de la sección 18.3 se copia como `.github/workflows/deploy-staging.yml` cambiando `environment: production` por `environment: staging` en los dos trabajos.

### 18.3 Flujo `.github/workflows/deploy.yml`

```
name: deploy
on:
  workflow_dispatch:
permissions:
  id-token: write
  contents: read
jobs:
  build:
    runs-on: ubuntu-latest
    environment: production
    strategy:
      matrix:
        svc: [SignupAPI, AdminAPI, Billing, BizCenter, CommissionEngine, RankEngine,
              TicketManagementSystem, SharedAPICenter, AdminWeb, BizCenterWeb, Signups]
    steps:
      - uses: actions/checkout@v4
      - uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: arn:aws:iam::${{ vars.AWS_ACCOUNT_ID }}:role/MLM-GitHubDeploy
          aws-region: ${{ vars.AWS_REGION }}
      - id: ecr
        uses: aws-actions/amazon-ecr-login@v2
      - run: |
          REPO=$(echo "${{ matrix.svc }}" | tr '[:upper:]' '[:lower:]')
          IMG=${{ steps.ecr.outputs.registry }}/mlmconqueror/$REPO:${GITHUB_SHA::7}
          docker build --build-arg PROJECT=MLMConquerorGlobalEdition.${{ matrix.svc }} -t $IMG .
          docker push $IMG
  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: arn:aws:iam::${{ vars.AWS_ACCOUNT_ID }}:role/MLM-GitHubDeploy
          aws-region: ${{ vars.AWS_REGION }}
      - run: aws eks update-kubeconfig --name ${{ vars.EKS_CLUSTER }}
      - run: |
          REG=${{ vars.AWS_ACCOUNT_ID }}.dkr.ecr.${{ vars.AWS_REGION }}.amazonaws.com
          for s in signupapi adminapi billing bizcenter rankengine commissionengine \
                   ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups; do
            kubectl set image deployment/$s -n mlmconqueror app=$REG/mlmconqueror/$s:${GITHUB_SHA::7}
            kubectl rollout status deployment/$s -n mlmconqueror --timeout=12m
          done
```

El flujo no ejecuta las pruebas. Varias suites necesitan un SQL Server propio y la solución incluye las apps MAUI, que no compilan en Linux. Las pruebas se corren en Windows antes de lanzarlo, igual que en la sección 2.7. El flujo se lanza a mano (Actions, deploy, Run workflow) sobre `master`.

El nombre del repositorio ECR coincide con el proyecto en minúsculas (`SignupAPI` da `signupapi`), por eso basta con `tr`.

**Antes de cada despliegue que traiga migraciones** hay que hacer, a mano y en este orden:
1. La instantánea de RDS (sección 16.5).
2. Aplicar el `migrate-rds.sql` regenerado (Fase 8, secciones 10.2 a 10.4).
3. Lanzar el flujo.

El flujo no migra la base a propósito.

**Deshacer un despliegue:**

```
kubectl rollout undo deployment/<servicio> -n $NS
```

Una migración aplicada no se deshace con esto. Para eso se restaura la instantánea tomada antes.

---

## 19. Apps MAUI (fuera de EKS, pero dependen de él)

Antes de publicar las apps en las tiendas:

| App | Archivo | Cambio |
|---|---|---|
| AdminApp | `MauiProgram.cs` líneas 49 y 55 | `https://localhost:7005` pasa a `https://auth-api.${DOMINIO}` (login y JWKS) |
| AdminApp | `MauiProgram.cs` líneas 80 y 88 | `https://localhost:7002` pasa a `https://admin-api.${DOMINIO}` |
| BizCenterApp | `MauiProgram.cs`, `AuthApiBaseUrl` por defecto | `https://auth-api.${DOMINIO}` |
| BizCenterApp | `MauiProgram.cs`, `SignupAppUrl` por defecto | `https://signup.${DOMINIO}/ambassador-join` |
| BizCenterApp | `MauiProgram.cs` líneas 58 y 78 | `https://localhost:7001` pasa a `https://api.${DOMINIO}`. Hoy apunta al puerto de AdminWeb, no al de BizCenter. |

---

## 20. Lista final de comprobación

| # | Comprobación | Cómo |
|---|---|---|
| 1 | Los 10 cambios de la Fase 0 están en el commit desplegado | `git log` y la tabla de la sección 2 |
| 2 | 11 servicios en Running, 2 réplicas cada uno | `kubectl get deploy -n $NS` |
| 3 | Cada pod arranca con sus líneas `[SecretosAws]`, con el entorno correcto, y sin `AccessDenied` ni ningún otro error de secretos | Sección 13.2, comprobación final |
| 4 | Un pod de un servicio no puede leer un secreto que no está en su fila: con la cuenta de servicio de billing, `jwt-firma` da `AccessDeniedException` | Sección 11.1, prueba con `amazon/aws-cli` |
| 5 | Ningún Secret de Kubernetes con valores de la aplicación y ningún secreto montado como archivo | `kubectl get secret -n $NS` no lista ninguno de la aplicación |
| 6 | 11 roles `MLM-$ENTORNO-<servicio>` y 11 cuentas de servicio anotadas con su rol | Secciones 8.8 y 11.1 |
| 7 | Salud pública 200 en los 6 nombres con `/health`, a través de CloudFront | Sección 15.5 |
| 8 | El ALB es interno y no responde desde internet; su grupo de seguridad solo admite la lista de prefijos de CloudFront | Secciones 14.3 y 15.5 |
| 9 | El WAF está asociado a la distribución de CloudFront | Sección 15.3 |
| 10 | La IP que guarda el servicio es la del cliente, no la de CloudFront ni la del ALB | Sección 15.5 |
| 11 | Swagger, Hangfire y `/api/v1/dev` responden 404 desde fuera | `curl` contra cada nombre |
| 12 | JWKS publica la llave nueva | `curl https://auth-api.${DOMINIO}/.well-known/jwks.json` |
| 13 | La sesión sobrevive a un reinicio del portal | Sección 15.6, punto 6 |
| 14 | SES fuera del sandbox y un correo real entregado | Secciones 7.7 y 15.6 |
| 15 | Los trabajos de Hangfire tienen servidor y próxima ejecución | Sección 16.2 |
| 16 | Alarmas creadas (también la de CloudFront) y suscripción confirmada | Sección 16.3 |
| 17 | Instantánea de RDS tras el esquema y copia de EFS activa | Secciones 10.5 y 7.5 |
| 18 | PFX y contraseña guardados fuera de AWS | Sección 8.4 |
| 19 | Interruptores de tarjeta en `false` y 2FA sin roles obligatorios hasta la Fase 15 | `kubectl get cm -n $NS -o yaml` |
| 20 | Credenciales expuestas en el repositorio rotadas | Sección 2.2 |
