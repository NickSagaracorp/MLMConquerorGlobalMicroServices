# Despliegue en Docker fuera de AWS — Guía paso a paso

**Fecha:** 20-nov-2026
**Código de referencia:** rama `secretos-aws`
**Alcance:** los 11 servicios de MLM Conqueror Global Edition en un servidor Linux con Docker que no está en AWS (en las instalaciones de la empresa o en otra nube), entorno `staging`. El mismo procedimiento sirve para `prod`, en la cuenta de AWS de producción. Los secretos viven en AWS Secrets Manager y cada servicio los lee con su propia identidad, por IAM Roles Anywhere. La entrada pública es Cloudflare Tunnel.
**Fuera del alcance:** la instalación del SQL Server y del Redis (son propios y se asumen funcionando), la carga de datos del sistema anterior y las apps MAUI. Para el despliegue en Amazon EKS está `docs/deployment/despliegue-aws-eks.md`.

---

## Cómo usar esta guía

- Las fases van en orden y cada una depende de las anteriores. No se salta ninguna.
- Cada fase empieza con tres apartados: **Qué se persigue** (el objetivo en lenguaje llano), **Por qué así** (por qué es la forma más segura o la que mejor escala) y **Alternativas descartadas** (qué otras formas hay y por qué no se eligieron). Después vienen los comandos.
- Cada paso dice qué hacer, el comando exacto y cómo comprobar que quedó bien. No se pasa al siguiente hasta que la comprobación da lo esperado.
- Hay dos máquinas y cada bloque de comandos dice en cuál va:
  - **Máquina de administración:** la del administrador, con el repositorio y el acceso a la cuenta de AWS. Terminal Linux (WSL2 con Ubuntu 24.04) o Git Bash en Windows; los scripts de `deploy/aws/` funcionan en las dos. Los comandos son bash; no se ejecutan en PowerShell.
  - **Servidor:** la máquina Linux donde corre Docker. Se entra por SSH.
- El entorno se elige con la variable `ENTORNO` (`staging` o `prod`). Ningún comando trae el entorno escrito a mano.
- Ningún secreto se escribe en el repositorio, en el compose, en `.env.servidor` ni en una variable que quede en el historial de la terminal. Los secretos van de un archivo temporal a Secrets Manager y el archivo se borra en el mismo paso.
- La cuenta de AWS de desarrollo (la de Nick) solo tiene los entornos `local` y `staging`. `prod` se crea siempre en la cuenta de producción, con un perfil de AWS de esa cuenta.

## Decisiones que hay que tener antes de empezar

| Decisión | Valor que asume la guía | Dónde se usa |
|---|---|---|
| Entorno | `staging` (para producción, `prod` en la cuenta de producción) | Nombres de secretos, roles, llave KMS y certificados |
| Región de AWS | `us-east-1` | Secrets Manager, KMS, IAM Roles Anywhere |
| Dominio | Una zona de Cloudflare propia del entorno, en `$DOMINIO`. Staging y prod no comparten nombres: cada entorno tiene su zona. | Nombres públicos, CORS, enlaces de correo |
| Servidor | Linux x86_64, Docker Engine 24 o superior, Docker Compose 2.23 o superior | Todo el despliegue |
| SQL Server y Redis | Propios, fuera del compose, alcanzables desde el servidor por la red privada | Secreto `datos` |
| Imágenes | Copia directa al servidor con `docker save` / `docker load`, o un registro (ECR u otro) | Fase 5 |
| Correo y SMS | Apagados (`Notifications__Email__Provider` y `Notifications__Sms__Provider` en `Null`) | Compose del servidor |
| Pago con tarjeta y Entrax | Apagados | Compose del servidor |

Si el dominio de staging es un subdominio (por ejemplo `staging.ejemplo.com`), los nombres públicos quedan en segundo nivel (`admin.staging.ejemplo.com`) y el certificado gratuito de Cloudflare (Universal SSL) no los cubre: hace falta un certificado avanzado de Cloudflare. Con una zona propia para staging no hay ese problema.

Nombres públicos (iguales en EKS y en Docker):

| Nombre | Servicio | Para qué es público |
|---|---|---|
| `admin.${DOMINIO}` | adminweb | Portal de administración |
| `app.${DOMINIO}` | bizcenterweb | Portal de miembros |
| `signup.${DOMINIO}` | signups | Aplicación de alta |
| `auth-api.${DOMINIO}` | signupapi | El navegador del alta, las apps MAUI, el JWKS y el callback de Entrax |
| `admin-api.${DOMINIO}` | adminapi | App MAUI de administración |
| `api.${DOMINIO}` | bizcenter | App MAUI del centro de negocios |
| `rank.${DOMINIO}` | rankengine | Solo `/certificates` (los PDF de reconocimiento) |
| `integrations.${DOMINIO}` | sharedapicenter | Solo `/api/v1/external` |

Billing, CommissionEngine y TicketManagementSystem nunca se publican. `/swagger`, `/hangfire`, `/api/v1/dev` y `/api/v1/webhooks/*` quedan bloqueados desde fuera.

---

## 1. Qué se despliega y qué se asume

**Qué se persigue.** Correr los 11 servicios en un solo servidor que no es de AWS, sin que ningún secreto quede escrito en ese servidor (salvo el token del túnel, que solo sirve para el túnel), sin abrir ningún puerto de entrada hacia internet, y con cada servicio limitado a leer solo los secretos que necesita.

**Por qué así.**
- **Secretos en AWS Secrets Manager, leídos por el propio servicio al arrancar.** El servidor no guarda contraseñas de base, llaves de firma ni licencias. Quien robe el disco del servidor o una copia de `/opt/mlmconqueror` no se lleva los secretos.
- **Una identidad de AWS por servicio (IAM Roles Anywhere).** Cada servicio presenta su propio certificado y recibe credenciales temporales de una hora de su propio rol. Si alguien toma el control del contenedor de Billing, solo puede leer los secretos de Billing.
- **Entrada por Cloudflare Tunnel.** El servidor abre la conexión hacia Cloudflare; desde internet no hay ninguna puerta abierta que escanear o atacar. El WAF y la protección contra ataques de denegación de servicio quedan delante, en Cloudflare.
- **Si falta un secreto, el servicio no arranca.** Un servicio de producción nunca arranca con los valores de desarrollo de `appsettings.json` sin que nadie se entere.

**Alternativas descartadas.**
- **Secretos en un `.env` o en variables del compose:** quedan en texto plano en el disco del servidor, en las copias de seguridad y en `docker inspect`. Cualquiera con acceso al servidor los lee todos.
- **Un usuario de IAM con claves fijas en el servidor:** las claves valen hasta que alguien las rota, y si se filtran sirven desde cualquier lugar del mundo. Además, con un solo usuario todos los servicios leerían todos los secretos.
- **Un contenedor de arranque que baje los secretos y se los pase a los demás:** sería una pieza más que mantener, distinta de la de EKS, y los secretos acabarían en un volumen compartido.
- **Seguir con los valores de `appsettings.json` si AWS falla:** un servicio arrancaría con la configuración equivocada y nadie lo notaría hasta ver datos mal.

### 1.1 Piezas

- **Servidor:** un compose (`deploy/docker/docker-compose.servidor.yml`) con 12 contenedores: los 11 servicios y `cloudflared`.
- **Redes de Docker:**
  - `borde` (subred fija `172.30.0.0/24`): `cloudflared` y los 8 servicios publicados.
  - `interna`: los 11 servicios. Billing, CommissionEngine y TicketManagementSystem solo están aquí, así que `cloudflared` no tiene forma de llegar a ellos.
- **Ningún puerto publicado:** el compose del servidor no tiene ninguna línea `ports`.
- **Identidad:** cada contenedor (menos signups, que no lee secretos) monta su `cert.pem` y su `key.pem` de solo lectura. La imagen trae `aws_signing_helper` 1.8.6, y el `AWS_CONFIG_FILE` de cada contenedor lo invoca con `credential_process`. El SDK de AWS obtiene así credenciales temporales de su rol `MLM-${ENTORNO}-<servicio>` y las renueva solo.
- **Secretos:** `mlmconqueror/${ENTORNO}/<secreto>` en Secrets Manager, cifrados con la llave KMS `alias/mlmconqueror-${ENTORNO}`.
- **Fuera del servidor:** el SQL Server y el Redis (propios, con sus copias de seguridad), la cuenta de AWS y la zona de Cloudflare.

### 1.2 El servidor

| Requisito | Valor |
|---|---|
| Sistema | Linux x86_64 (Ubuntu 24.04 o equivalente) |
| Docker | Docker Engine 24 o superior y el plugin Docker Compose 2.23 o superior. El compose usa `configs` con `content`, que no existe antes de la 2.23. |
| Memoria y CPU | 16 GB y 4 vCPU como mínimo para los 11 servicios |
| Puertos de entrada | Ninguno abierto hacia internet. El acceso de administración (SSH) solo desde la red privada o una VPN. |
| Reloj | Sincronizado por NTP. Roles Anywhere rechaza las firmas con más de 5 minutos de desfase y, sin reloj correcto, ningún servicio arranca. |
| Herramientas | AWS CLI v2 y `jq`, para bajar el token del túnel y para las comprobaciones |
| Acceso a la base | El servidor llega al SQL Server (1433) y al Redis (6379) por la red privada |

### 1.3 Salida a internet que necesita el servidor

| Destino | Puerto | Para qué |
|---|---|---|
| `*.argotunnel.com` (región de Cloudflare) | 7844 TCP y UDP | Conexión de `cloudflared` con Cloudflare |
| `secretsmanager.${AWS_REGION}.amazonaws.com` | 443 | Leer los secretos al arrancar |
| `rolesanywhere.${AWS_REGION}.amazonaws.com` | 443 | Cambiar el certificado de cada servicio por credenciales temporales |
| `sts.amazonaws.com` | 443 | Comprobaciones con el AWS CLI |
| `registry-1.docker.io` y `production.cloudflare.docker.com` | 443 | Bajar la imagen de `cloudflared` |
| El registro de imágenes, si se usa | 443 | Bajar las imágenes de los servicios (Fase 5) |
| `s3.${AWS_REGION}.amazonaws.com` y `email.${AWS_REGION}.amazonaws.com` | 443 | Solo si el entorno usa S3 o SES |

Todo lo demás de salida se puede cerrar. Nada de entrada se abre.

### 1.4 Qué secreto lee cada servicio

La tabla única es `deploy/aws/secretos-por-servicio.json`. De ella salen los permisos de cada rol, y una prueba automática comprueba que la lista `SecretosAws:Secretos` del `appsettings.json` de cada servicio coincide con su fila.

| Servicio | Secretos | Certificado |
|---|---|---|
| adminapi | `datos`, `jwt-firma`, `dp-gateway`, `twilio` | Sí |
| adminweb | `portales` | Sí |
| billing | `datos`, `dp-gateway`, `billing`, `firebase`, `twilio` | Sí |
| bizcenter | `datos`, `dp-gateway`, `firebase`, `twilio` | Sí |
| bizcenterweb | `portales` | Sí |
| commissionengine | `datos`, `firebase` | Sí |
| rankengine | `datos`, `firebase`, `twilio` | Sí |
| sharedapicenter | `datos`, `sharedapicenter`, `firebase` | Sí |
| signupapi | `datos`, `jwt-firma`, `signupapi`, `firebase`, `twilio` | Sí |
| signups | Ninguno: no llama a AWS | No |
| ticketmanagementsystem | `datos`, `firebase` | Sí |

`borde` (el token del túnel) no lo lee ningún servicio: lo baja `deploy/docker/bajar-token-tunel.sh` con la credencial del administrador.

### 1.5 Cómo se comporta un servicio al arrancar

- Lee la sección `SecretosAws` de su configuración. En el compose del servidor, `SecretosAws__Entorno` vale `${ENTORNO}`.
- Pide a Secrets Manager `mlmconqueror/${ENTORNO}/<secreto>` para cada nombre de su lista y escribe en el log una línea por secreto, con el número de claves y nunca los valores:

```
[SecretosAws] mlmconqueror/staging/datos: 3 clave(s).
```

- Cada clave `Seccion__Clave` del JSON pasa a `Seccion:Clave` de la configuración. Los secretos ganan sobre `appsettings.json` y pierden contra una variable de entorno: en una emergencia se puede forzar un valor desde el compose sin tocar AWS.
- Si un secreto falla, el servicio **no arranca**, y el mensaje dice cuál y por qué:

| Mensaje | Causa en Docker |
|---|---|
| `La identidad de este servicio no tiene permiso sobre '...' (o sobre la llave KMS del entorno).` | El rol no tiene ese secreto en su política: falta en `secretos-por-servicio.json` o no se volvió a correr `crear-roles.sh` |
| `El secreto '...' no existe en ese entorno.` | No se corrió `crear-kms-y-secretos.sh` en ese entorno, o `ENTORNO` está mal en `.env.servidor` |
| `Este servicio no tiene identidad de AWS: ...` | Falta el certificado o la llave, no tienen permisos para el usuario 1654, el reloj está desfasado, el certificado venció o fue revocado, o el certificado es de otro servicio. El texto `Detalle:` trae el error de `aws_signing_helper`. |
| `'...' no es un JSON de pares clave-texto.` | El secreto se cargó con un valor que no es un objeto JSON de textos |
| `Entorno '...' no válido: tiene que ser local, staging o prod.` | `ENTORNO` vacío o mal escrito |

---

## 2. Fase 1 — Herramientas y acceso

**Qué se persigue.** Preparar la máquina desde la que se administra todo, darle al administrador un permiso de AWS que alcance justo para esta guía, y dejar el servidor listo para recibir el despliegue.

**Por qué así.**
- **La máquina de administración es la única que habla con IAM, KMS y la autoridad certificadora.** El servidor nunca tiene permiso para crear roles, cambiar secretos ni firmar certificados: si alguien lo compromete, no puede ampliar sus propios permisos.
- **El administrador tiene un permiso mínimo y sin poder asumir los roles de los servicios.** Puede crear y cambiar los roles `MLM-${ENTORNO}-*`, pero una regla de denegación explícita le impide hacerse pasar por ellos, y además la confianza de cada rol solo admite al servicio de Roles Anywhere. Así, ni siquiera con la credencial del administrador se obtiene la identidad de Billing sin su certificado.
- **En el servidor, el administrador entra con una sesión temporal** (IAM Identity Center, `aws sso login`) que caduca sola, y la cierra al terminar.

**Alternativas descartadas.**
- **Administrar desde el propio servidor:** pondría la llave de la autoridad certificadora y una credencial con permiso sobre IAM en la misma máquina que expone los servicios.
- **Usar una cuenta con `AdministratorAccess`:** funciona, pero un error de dedo o una credencial filtrada alcanza toda la cuenta, incluidos otros entornos.
- **Claves de acceso fijas del administrador en el servidor:** valen hasta que alguien las rota y se quedan en `~/.aws/credentials`.

### 2.1 Máquina de administración

En WSL2 (Ubuntu 24.04):

```
# AWS CLI v2
curl -s "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o /tmp/awscliv2.zip
unzip -q /tmp/awscliv2.zip -d /tmp && sudo /tmp/aws/install && rm -rf /tmp/aws /tmp/awscliv2.zip
# jq y openssl
sudo apt-get update && sudo apt-get install -y jq openssl
aws --version && jq --version && openssl version
```

En Git Bash de Windows: AWS CLI v2 con el instalador MSI de AWS, `jq` con `winget install jqlang.jq`; `openssl` ya viene con Git para Windows.

Para construir las imágenes (Fase 5) hace falta además Docker en esta máquina.

**Comprobar:** `aws --version` empieza por `aws-cli/2`.

### 2.2 Perfil de AWS del administrador

Con IAM Identity Center (lo recomendado):

```
aws configure sso --profile mlm-admin-staging
aws sso login --profile mlm-admin-staging
export AWS_PROFILE=mlm-admin-staging
aws sts get-caller-identity
```

**Comprobar:** `Account` es la cuenta del entorno. Para `prod`, la cuenta de producción, nunca la de desarrollo.

### 2.3 Permiso mínimo del administrador

Lo aplica una vez alguien con permisos de IAM de la cuenta, para el grupo o el *permission set* de quien va a desplegar. Se genera con las variables del entorno:

```
export ENTORNO=staging AWS_REGION=us-east-1
CUENTA=$(aws sts get-caller-identity --query Account --output text)
P="arn:aws:secretsmanager:$AWS_REGION:$CUENTA:secret:mlmconqueror/$ENTORNO"
cat > /tmp/politica-admin.json <<EOF
{"Version":"2012-10-17","Statement":[
 {"Sid":"KmsCrear","Effect":"Allow","Action":["kms:CreateKey","kms:ListAliases","kms:TagResource"],
  "Resource":"*"},
 {"Sid":"KmsAlias","Effect":"Allow","Action":"kms:CreateAlias","Resource":[
  "arn:aws:kms:$AWS_REGION:$CUENTA:alias/mlmconqueror-$ENTORNO",
  "arn:aws:kms:$AWS_REGION:$CUENTA:key/*"]},
 {"Sid":"KmsLlaveDelEntorno","Effect":"Allow","Action":["kms:DescribeKey","kms:EnableKeyRotation",
  "kms:GetKeyRotationStatus","kms:Encrypt","kms:Decrypt","kms:GenerateDataKey"],
  "Resource":"arn:aws:kms:$AWS_REGION:$CUENTA:key/*",
  "Condition":{"ForAnyValue:StringEquals":{"kms:ResourceAliases":"alias/mlmconqueror-$ENTORNO"}}},
 {"Sid":"Secretos","Effect":"Allow","Action":["secretsmanager:CreateSecret",
  "secretsmanager:DescribeSecret","secretsmanager:PutSecretValue","secretsmanager:TagResource"],
  "Resource":"$P/*"},
 {"Sid":"LeerSoloDatosYBorde","Effect":"Allow","Action":"secretsmanager:GetSecretValue",
  "Resource":["$P/datos-??????","$P/borde-??????"]},
 {"Sid":"ListarSecretos","Effect":"Allow","Action":"secretsmanager:ListSecrets","Resource":"*"},
 {"Sid":"RolesDeLosServicios","Effect":"Allow","Action":["iam:GetRole","iam:CreateRole",
  "iam:TagRole","iam:UpdateAssumeRolePolicy","iam:PutRolePolicy","iam:GetRolePolicy",
  "iam:DeleteRolePolicy"],"Resource":"arn:aws:iam::$CUENTA:role/MLM-$ENTORNO-*"},
 {"Sid":"PasarRolesSoloARolesAnywhere","Effect":"Allow","Action":"iam:PassRole",
  "Resource":"arn:aws:iam::$CUENTA:role/MLM-$ENTORNO-*",
  "Condition":{"StringEquals":{"iam:PassedToService":"rolesanywhere.amazonaws.com"}}},
 {"Sid":"RolesAnywhere","Effect":"Allow","Action":["rolesanywhere:ListTrustAnchors",
  "rolesanywhere:GetTrustAnchor","rolesanywhere:CreateTrustAnchor",
  "rolesanywhere:EnableTrustAnchor","rolesanywhere:DisableTrustAnchor","rolesanywhere:ListProfiles",
  "rolesanywhere:GetProfile","rolesanywhere:CreateProfile","rolesanywhere:UpdateProfile",
  "rolesanywhere:ImportCrl",
  "rolesanywhere:ListCrls","rolesanywhere:EnableCrl","rolesanywhere:DeleteCrl",
  "rolesanywhere:TagResource"],"Resource":"*"},
 {"Sid":"NuncaAsumirLosRolesDeLosServicios","Effect":"Deny","Action":"sts:AssumeRole",
  "Resource":"arn:aws:iam::$CUENTA:role/MLM-*"}]}
EOF
jq . /tmp/politica-admin.json >/dev/null && echo "JSON valido"
```

Se aplica a un grupo de IAM (o se pega como política en línea del *permission set* de IAM Identity Center):

```
aws iam put-group-policy --group-name mlm-admin-$ENTORNO --policy-name mlmconqueror-despliegue \
  --policy-document file:///tmp/politica-admin.json
rm -f /tmp/politica-admin.json
```

Qué permite y qué no:

| Puede | No puede |
|---|---|
| Crear la llave KMS del entorno, su alias y su rotación | Usar llaves KMS de otros entornos |
| Crear los secretos del entorno y cambiar su valor | Leer los secretos, salvo `datos` (para crear el usuario de la base) y `borde` (para el túnel) |
| Crear y cambiar los roles `MLM-${ENTORNO}-*` y sus políticas | Crear otros roles ni tocar otros entornos |
| Crear el trust anchor, el perfil y las listas de revocación de Roles Anywhere | Asumir ningún rol `MLM-*` |

Si las imágenes se suben a ECR (Fase 5, opción B), el mismo grupo necesita además:

```
{"Sid":"Ecr","Effect":"Allow","Action":["ecr:CreateRepository","ecr:DescribeRepositories",
 "ecr:BatchCheckLayerAvailability","ecr:InitiateLayerUpload","ecr:UploadLayerPart",
 "ecr:CompleteLayerUpload","ecr:PutImage","ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"],
 "Resource":"arn:aws:ecr:$AWS_REGION:$CUENTA:repository/mlmconqueror/*"},
{"Sid":"EcrLogin","Effect":"Allow","Action":"ecr:GetAuthorizationToken","Resource":"*"}
```

### 2.4 Archivo de variables de la guía

En la máquina de administración. Todos los comandos de la guía las usan:

```
cat > ~/mlm-docker-staging.env <<'EOF'
export ENTORNO=staging
export AWS_PROFILE=mlm-admin-staging
export AWS_REGION=us-east-1
export DOMINIO=ejemplo-staging.com          # la zona de Cloudflare del entorno
export SERVIDOR=admin@10.0.0.20             # usuario y direccion SSH del servidor
export REPO=~/src/MLMConquerorGlobalMicroServices
EOF
echo "export CUENTA=$(aws sts get-caller-identity --query Account --output text)" \
  >> ~/mlm-docker-staging.env
source ~/mlm-docker-staging.env
```

Cada vez que se abre una terminal nueva: `source ~/mlm-docker-staging.env`. Para producción se hace un `~/mlm-docker-prod.env` con `ENTORNO=prod` y el perfil de la cuenta de producción.

Copia limpia del repositorio, en el commit que se va a desplegar:

```
cd $REPO && git fetch && git status --short     # sin cambios locales
```

### 2.5 El servidor

En el servidor:

```
docker version --format '{{.Server.Version}}'    # 24 o superior
docker compose version --short                   # 2.23 o superior
timedatectl | grep -E "synchronized|NTP"         # System clock synchronized: yes
sudo apt-get update && sudo apt-get install -y jq unzip
curl -s "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o /tmp/awscliv2.zip
unzip -q /tmp/awscliv2.zip -d /tmp && sudo /tmp/aws/install && rm -rf /tmp/aws /tmp/awscliv2.zip
sudo mkdir -p /opt/mlmconqueror && sudo chown "$USER" /opt/mlmconqueror
sudo usermod -aG docker "$USER"                  # volver a entrar por SSH despues
```

Si `timedatectl` dice `System clock synchronized: no`: `sudo timedatectl set-ntp true` y esperar un minuto.

Sesión temporal del administrador en el servidor (se usa en las Fases 6, 7 y 9 y en la operación):

```
aws configure sso --profile mlm-admin-staging      # una vez
aws sso login --profile mlm-admin-staging --use-device-code
export AWS_PROFILE=mlm-admin-staging AWS_REGION=us-east-1
```

Al terminar cada sesión de trabajo: `aws sso logout`. La sesión caduca sola aunque se olvide.

**Comprobar desde fuera** (desde una máquina que no esté en la red del servidor):

```
nc -zv -w5 <IP publica del servidor> 22 80 443 8080    # todos tienen que fallar
```

---

## 3. Fase 2 — Llave KMS y secretos del entorno

**Qué se persigue.** Crear la llave que cifra los secretos del entorno y los 10 secretos del entorno, cada uno con su contenido.

**Por qué así.**
- **Una llave KMS por entorno, con rotación anual.** Los secretos de staging y los de prod están cifrados con llaves distintas: un permiso de descifrar sobre la llave de staging no sirve para prod. La rotación anual la hace AWS sola y no obliga a volver a cifrar nada.
- **Secretos agrupados por tema, no por servicio.** La cadena de la base vive en un solo secreto (`datos`) y la leen los 8 servicios que la usan. Cambiar la contraseña de SQL es cambiar un secreto, no nueve.
- **Las claves del JSON son los nombres de configuración de .NET** (`Seccion__Clave`). No hay traducción intermedia ni mapeos que mantener.
- **Del archivo temporal al secreto, y el archivo se borra en el mismo paso.** El valor no queda en el disco de la máquina de administración ni en el historial de la terminal.

**Alternativas descartadas.**
- **La llave administrada por AWS (`aws/secretsmanager`):** no se puede limitar por entorno con una política propia, y cualquiera con `GetSecretValue` en la cuenta la usa sin más.
- **Un secreto por servicio:** la cadena de SQL se repetiría en 8 sitios y un cambio olvidado en uno dejaría un servicio sin base.
- **AWS Systems Manager Parameter Store:** sirve, pero no tiene versiones con etiquetas ni la rotación con Lambda que se puede agregar después en Secrets Manager.

### 3.1 Llave y secretos vacíos

En la máquina de administración:

```
source ~/mlm-docker-staging.env && cd $REPO
bash deploy/aws/crear-kms-y-secretos.sh $ENTORNO
```

**Comprobar:** la salida dice `Llave KMS alias/mlmconqueror-staging creada` (o nada, si ya existía) y `creado:` o `ya existe:` para los 10: `datos`, `jwt-firma`, `dp-gateway`, `signupapi`, `billing`, `sharedapicenter`, `portales`, `firebase`, `twilio` y `borde`.

```
aws kms get-key-rotation-status --key-id alias/mlmconqueror-$ENTORNO \
  --query KeyRotationEnabled     # true
```

Los secretos nacen vacíos (`{}`). Un servicio que arranque ahora no falla por permiso, pero no tiene sus valores.

### 3.2 Carpeta de trabajo temporal

```
umask 077
mkdir -p ~/mlm-secretos && cd ~/mlm-secretos
set +o history      # no guardar en el historial lo que se escriba en esta fase
cargar() { bash $REPO/deploy/aws/cargar-secreto.sh $ENTORNO "$1" "$2" && shred -u "$2"; }
```

`cargar <secreto> <archivo>` valida que el archivo sea un objeto JSON de textos, lo carga, lista los servicios que lo leen y borra el archivo.

### 3.3 `datos`: base de datos y Redis

Claves: `ConnectionStrings__DefaultConnection`, `ConnectionStrings__HangFire`, `ConnectionStrings__Redis`.

La contraseña de `mlm_app` (el usuario de la aplicación, que se crea en la Fase 6) se genera aquí:

```
read -p "Nombre o IP privada del SQL Server: " SQL_HOST
read -p "Nombre o IP privada del Redis: " REDIS_HOST
read -s -p "Contrasena del Redis (vacio si no tiene): " REDIS_PASS; echo
openssl rand -base64 36 | tr -d '/+=;' | cut -c1-40 > sql-app-pass
SQL="Server=$SQL_HOST,1433;Database=MLMConquerorGlobalEditionDb;User Id=mlm_app;\
Password=$(cat sql-app-pass);Encrypt=True;TrustServerCertificate=True;\
MultipleActiveResultSets=True;Connect Timeout=30"
REDIS="$REDIS_HOST:6379,abortConnect=False${REDIS_PASS:+,password=$REDIS_PASS}"
jq -n --arg s "$SQL" --arg r "$REDIS" '{
  "ConnectionStrings__DefaultConnection": $s,
  "ConnectionStrings__HangFire": $s,
  "ConnectionStrings__Redis": $r }' > datos.json
cargar datos datos.json
unset SQL REDIS REDIS_PASS
```

`SQL_HOST` no puede ser `localhost`: dentro de un contenedor, `localhost` es el propio contenedor. Si el SQL Server está en el mismo servidor, se usa la IP privada del servidor y el firewall del servidor admite el 1433 solo desde las redes de Docker.

`TrustServerCertificate=True` cifra la conexión pero no valida el certificado del SQL Server, porque la imagen solo confía en las autoridades públicas y en la de RDS. Si el SQL Server tiene un certificado de una autoridad pública, se cambia a `False`. Si el Redis tiene TLS, se agrega `,ssl=True` a la cadena.

### 3.4 `jwt-firma`: llave de firma de los tokens

Clave: `Jwt__PrivateKeyBase64`. Formato **PKCS#8 DER en base64**, de 2048 bits, el mismo de `docs/deployment/rotacion-llaves-jwt.md`.

Si `deploy/aws/llaves-publicas/${ENTORNO}-jwt-publica.b64` ya existe en el repositorio, el par de este entorno ya se generó y la privada ya está en `jwt-firma`: no se regenera (eso es una rotación, con su propia guía). Si no existe:

```
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out jwt-privada.pem
openssl pkcs8 -topk8 -nocrypt -in jwt-privada.pem -outform DER -out jwt-privada.der
openssl asn1parse -inform DER -in jwt-privada.der | head -4   # debe aparecer rsaEncryption
openssl rsa -inform DER -in jwt-privada.der -pubout -outform DER -out jwt-publica.der
jq -n --arg k "$(base64 -w0 jwt-privada.der)" '{"Jwt__PrivateKeyBase64": $k}' > jwt.json
cargar jwt-firma jwt.json
base64 -w0 jwt-publica.der > $REPO/deploy/aws/llaves-publicas/$ENTORNO-jwt-publica.b64
shred -u jwt-privada.pem jwt-privada.der jwt-publica.der
```

El archivo de la llave pública se sube al repositorio en un commit propio.

El paso por `pkcs8 -topk8` no es opcional: el openssl de Git para Windows saca la llave en PKCS#1 aunque se use `genpkey`, y SignupAPI no arranca con ella. La pública no es secreta: se sube al repositorio y va a `JWT_PUBLIC` en `.env.servidor` (Fase 8).

### 3.5 `dp-gateway`: anillo de credenciales de las pasarelas

Claves: `DataProtection__Certificate__Base64`, `DataProtection__Certificate__Password`. Durante una rotación del certificado se agregan los anteriores como `DataProtection__Certificate__Retired__0__Base64` y `DataProtection__Certificate__Retired__0__Password` (y `__1__`, etc.).

AdminAPI cifra con este PFX las credenciales de las pasarelas (PayQuicker, i-Payout, Volet, Spreedly, Entrax) que se guardan en la base; Billing y BizCenter las descifran. Los tres leen el mismo secreto. Si el PFX se pierde, todas las credenciales guardadas quedan ilegibles.

```
openssl rand -base64 32 | tr -d '/+=' > pfx-pass
openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
  -keyout dp.key -out dp.crt -subj "/CN=MLMConqueror Gateway Credentials $ENTORNO" \
  -addext "keyUsage=critical,keyEncipherment,dataEncipherment"
openssl pkcs12 -export -inkey dp.key -in dp.crt -out gateway.pfx \
  -passout file:pfx-pass -certpbe AES-256-CBC -keypbe AES-256-CBC -macalg sha256
jq -n --arg b "$(base64 -w0 gateway.pfx)" --arg p "$(cat pfx-pass)" '{
  "DataProtection__Certificate__Base64": $b,
  "DataProtection__Certificate__Password": $p }' > dp.json
cargar dp-gateway dp.json
```

**Antes de seguir:** se guarda una copia de `gateway.pfx` y `pfx-pass` en el gestor de contraseñas de la empresa, fuera de AWS. Es la única forma de recuperar las credenciales si alguien borra el secreto. Cada entorno tiene su propio PFX.

### 3.6 `signupapi`: cuenta raíz y Entrax

Claves: `RootAmbassador__Email`, `RootAmbassador__Password`, `Entrax__KeyId`, `Entrax__Secret`.

La cuenta raíz la siembra SignupAPI al arrancar. El correo es un buzón real de la empresa, nunca uno público. Entrax queda vacío mientras esté apagado:

```
read -p "Correo de la cuenta raiz: " ROOT_MAIL
openssl rand -base64 24 | tr -d '/+=' > root-pass
jq -n --arg e "$ROOT_MAIL" --arg p "$(cat root-pass)Aa1#" '{
  "RootAmbassador__Email": $e, "RootAmbassador__Password": $p,
  "Entrax__KeyId": "", "Entrax__Secret": "" }' > signupapi.json
cargar signupapi signupapi.json
```

La contraseña de la cuenta raíz (el contenido de `root-pass` seguido de `Aa1#`) se guarda en el gestor de contraseñas. Se cambia en la Fase 9.

### 3.7 `billing`: Stripe

Clave: `Stripe__SecretKey`. Mientras no haya cuenta de Stripe, vacía:

```
read -s -p "Llave secreta de Stripe (vacio si no hay cuenta): " STRIPE; echo
jq -n --arg k "$STRIPE" '{"Stripe__SecretKey": $k}' > billing.json
cargar billing billing.json
unset STRIPE
```

### 3.8 `sharedapicenter`: llave de la API externa

Clave: `ExternalApi__ApiKey` (la cabecera `X-Api-Key` de `/api/v1/external`):

```
jq -n --arg k "$(openssl rand -hex 32)" '{"ExternalApi__ApiKey": $k}' > ext.json
cargar sharedapicenter ext.json
```

Quien integre con esa API recibe la llave por un canal seguro; se lee con la consola de AWS por alguien con permiso sobre ese secreto.

### 3.9 `portales`: licencia de Syncfusion

Clave: `Syncfusion__LicenseKey`:

```
read -s -p "Clave de licencia de Syncfusion: " SF; echo
jq -n --arg k "$SF" '{"Syncfusion__LicenseKey": $k}' > portales.json
cargar portales portales.json
unset SF
```

### 3.10 `firebase`: notificaciones push

Clave: `Firebase__CredentialsJsonBase64`, el JSON de la cuenta de servicio de Firebase en base64. Se copia antes a `~/mlm-secretos/firebase.json` el JSON descargado de la consola de Firebase (Configuración del proyecto, Cuentas de servicio):

```
jq -n --arg j "$(base64 -w0 firebase.json)" '{"Firebase__CredentialsJsonBase64": $j}' > fb.json
cargar firebase fb.json && shred -u firebase.json
```

Si no hay proyecto de Firebase, se carga con el valor vacío. Los servicios arrancan igual y el push queda apagado:

```
jq -n '{"Firebase__CredentialsJsonBase64": ""}' > fb.json && cargar firebase fb.json
```

### 3.11 `twilio`: SMS

Claves: `Notifications__Sms__Twilio__AccountSid`, `Notifications__Sms__Twilio__AuthToken`. Mientras el SMS esté apagado, vacías:

```
jq -n '{"Notifications__Sms__Twilio__AccountSid": "",
        "Notifications__Sms__Twilio__AuthToken": ""}' > twilio.json
cargar twilio twilio.json
```

### 3.12 `borde`: token del túnel

Clave: `Cloudflare__TunnelToken`. Se carga en la Fase 7, cuando exista el túnel.

### 3.13 Borrar la carpeta de trabajo

```
cd ~ && shred -u ~/mlm-secretos/* 2>/dev/null; rm -rf ~/mlm-secretos
unset ROOT_MAIL SQL_HOST REDIS_HOST
set -o history
for s in datos jwt-firma dp-gateway signupapi billing sharedapicenter portales firebase twilio borde
do
  printf "%-16s" $s
  aws secretsmanager describe-secret --secret-id mlmconqueror/$ENTORNO/$s \
    --query LastChangedDate --output text
done
```

**Comprobar:** los 9 primeros muestran la fecha y hora de hoy (la de la carga). `borde` muestra la de su creación: se carga en la Fase 7. La contraseña de `mlm_app` hace falta en la Fase 6 y se lee del secreto `datos` en ese momento.

---

## 4. Fase 3 — Autoridad certificadora y Roles Anywhere

**Qué se persigue.** Que AWS confíe en los certificados que emite la empresa, y crear un rol por servicio que solo puede asumir quien presente el certificado de ese servicio.

**Por qué así.**
- **IAM Roles Anywhere cambia un certificado por credenciales temporales de una hora.** En el servidor no hay ninguna clave de acceso de AWS que valga para siempre. Si el certificado se roba, se revoca (sección 11.4) y deja de servir sin tocar los demás servicios.
- **Un rol por servicio, atado al nombre del certificado.** La confianza del rol `MLM-${ENTORNO}-billing` exige `CN=billing` y el trust anchor del entorno. El certificado de Billing no puede asumir el rol de SignupAPI, y uno de staging no sirve en prod.
- **Cada rol solo puede leer los secretos de su fila** de `secretos-por-servicio.json` y descifrar solo con la llave de su entorno, y solo a través de Secrets Manager.
- **La llave privada de la autoridad no vive en el servidor.** Con ella se puede emitir un certificado para cualquier servicio. Si estuviera en el servidor, quien lo comprometiera podría hacerse un certificado de SignupAPI y leer la llave de firma JWT. Se guarda fuera (un USB cifrado o el gestor de secretos de la empresa) y solo se trae a la máquina de administración para emitir, renovar o revocar.

**Alternativas descartadas.**
- **Usuarios de IAM con claves fijas, uno por servicio:** 10 pares de claves que valen hasta que alguien los rota, que se copian a un archivo del servidor y que sirven desde cualquier lugar si se filtran.
- **AWS Private CA:** hace lo mismo con la autoridad dentro de AWS, pero cuesta unos 400 dólares al mes por autoridad. Para 10 certificados al año, una autoridad propia con openssl, guardada fuera de línea, es igual de segura.
- **Un solo rol para todos los servicios:** el servicio con menos protección tendría acceso a la llave de firma JWT y al anillo de las pasarelas.

En la máquina de administración, en este orden:

```
source ~/mlm-docker-staging.env && cd $REPO
bash deploy/aws/roles-anywhere.sh $ENTORNO
bash deploy/aws/crear-roles.sh $ENTORNO docker
bash deploy/aws/roles-anywhere.sh $ENTORNO
```

1. La primera vez, `roles-anywhere.sh` crea la autoridad en `deploy/aws/ca/${ENTORNO}/` (`ca.key` y `ca.crt`, válidos 10 años), la registra como trust anchor `mlmconqueror-${ENTORNO}` y crea el perfil `mlmconqueror-${ENTORNO}` con sesiones de una hora.
2. `crear-roles.sh` crea o actualiza los 11 roles `MLM-${ENTORNO}-<servicio>` con su confianza (Roles Anywhere, `CN` del servicio y el trust anchor) y su política (`GetSecretValue` sobre sus secretos y `kms:Decrypt` sobre la llave del entorno). Signups queda sin política porque no lee nada.
3. La segunda pasada de `roles-anywhere.sh` agrega al perfil los roles que ya existen.

Si el entorno va a usar S3 o SES, el paso 2 se corre con el bucket y el remitente: `BUCKET=<bucket> SES_FROM=<remitente> bash deploy/aws/crear-roles.sh $ENTORNO docker`. Sin esas variables, los roles solo tienen secretos.

La última pasada imprime dos líneas que van a `.env.servidor` (Fase 8):

```
TRUST_ANCHOR_ARN=arn:aws:rolesanywhere:us-east-1:<cuenta>:trust-anchor/<id>
PROFILE_ARN=arn:aws:rolesanywhere:us-east-1:<cuenta>:profile/<id>
```

**Comprobar:**

```
aws iam get-role --role-name MLM-$ENTORNO-billing \
  --query 'Role.AssumeRolePolicyDocument.Statement[0].Condition' --output json
aws iam get-role-policy --role-name MLM-$ENTORNO-billing --policy-name secretos-y-aws \
  --query 'PolicyDocument.Statement[0].Resource' --output json
```

La primera muestra `"aws:PrincipalTag/x509Subject/CN": "billing"` y el ARN del trust anchor. La segunda, los 5 secretos de Billing (`datos`, `dp-gateway`, `billing`, `firebase`, `twilio`) y ningún otro.

**Guardar la autoridad:** al terminar la Fase 4, se copia `deploy/aws/ca/${ENTORNO}/` al USB cifrado o al gestor de secretos y se borra de la máquina (`shred -u deploy/aws/ca/$ENTORNO/ca.key`). La carpeta está en `.gitignore`, pero no debe quedarse en un portátil. Se vuelve a traer para renovar o revocar certificados.

---

## 5. Fase 4 — Certificados de los servicios

**Qué se persigue.** Emitir un certificado por servicio, llevarlo al servidor y dejarlo legible solo para el usuario con el que corren los servicios.

**Por qué así.**
- **Un certificado por servicio, válido un año.** Si se compromete uno, se revoca uno. La validez de un año limita el daño de un certificado olvidado y obliga a una renovación anual, que es un procedimiento corto.
- **La llave privada de cada servicio se genera en la máquina de administración y solo se copia al servidor.** Después se borra de la máquina de administración: la única copia útil está donde se usa.
- **Permisos 600 y dueño 1654** (el usuario `app` de la imagen de .NET). Ningún otro usuario del servidor puede leer las llaves, y dentro del contenedor solo el proceso del servicio las lee. Se montan de solo lectura: el servicio no puede cambiarlas.

**Alternativas descartadas.**
- **Un certificado comodín para todos los servicios:** cualquier servicio podría asumir cualquier rol.
- **Generar la llave en el servidor y mandar solo una solicitud de firma:** más correcto en teoría, pero obliga a traer la autoridad al servidor o a un ida y vuelta por cada servicio. Con la autoridad fuera de línea y las llaves borradas de la máquina de administración tras copiarlas, el riesgo es el mismo.
- **Guardar las llaves en un volumen de Docker:** los volúmenes se copian enteros en las copias de seguridad del servidor y no se ven a simple vista.

### 5.1 Emitir

En la máquina de administración:

```
source ~/mlm-docker-staging.env && cd $REPO
bash deploy/aws/emitir-certificados.sh $ENTORNO
```

**Comprobar:** una línea por servicio con `notAfter=` un año después, y `signups: no usa AWS, sin certificado`. Quedan 10 carpetas en `deploy/aws/certificados/${ENTORNO}/`, cada una con `cert.pem` y `key.pem`.

### 5.2 Copiar al servidor

```
scp -r deploy/aws/certificados/$ENTORNO $SERVIDOR:certificados-nuevos
```

En el servidor:

```
cd /opt/mlmconqueror/deploy/docker 2>/dev/null || mkdir -p /opt/mlmconqueror/deploy/docker
cd /opt/mlmconqueror/deploy/docker
sudo rm -rf certificados.anterior
[ -d certificados ] && sudo mv certificados certificados.anterior
sudo mv ~/certificados-nuevos certificados
sudo chown -R 1654:1654 certificados
sudo find certificados -type d -exec chmod 700 {} +
sudo find certificados -type f -exec chmod 600 {} +
sudo ls -ln certificados/billing
```

**Comprobar:** `cert.pem` y `key.pem` con `-rw-------` y dueño `1654 1654`. El usuario del servidor ya no puede leerlos sin `sudo`: es lo esperado.

### 5.3 Archivar los certificados y borrar las llaves de la máquina de administración

Los `cert.pem` (públicos) se archivan junto a la autoridad: hacen falta para revocar un certificado aunque ya se haya renovado. Las llaves se borran.

```
cd $REPO/deploy/aws
mkdir -p ca/$ENTORNO/emitidos
for d in certificados/$ENTORNO/*/; do
  n=$(basename $d); cp $d/cert.pem ca/$ENTORNO/emitidos/$n-$(date +%Y%m%d).pem
done
shred -u certificados/$ENTORNO/*/key.pem && rm -rf certificados/$ENTORNO
ls ca/$ENTORNO/emitidos
```

Después se guarda `deploy/aws/ca/${ENTORNO}/` fuera de la máquina (Fase 3).

---

## 6. Fase 5 — Imágenes

**Qué se persigue.** Llevar al servidor las 11 imágenes del commit que se despliega, con una etiqueta que diga de qué código salen.

**Por qué así.**
- **Una sola receta (`Dockerfile`) para los 11 servicios y la misma imagen que va a EKS.** Lo que se probó en un sitio es lo que corre en el otro. La imagen trae `aws_signing_helper` 1.8.6 comprobado con su SHA-256, y corre con el usuario sin privilegios 1654.
- **La etiqueta es el commit corto.** Cada contenedor dice de qué código sale, y volver atrás es cambiar la etiqueta.
- **Con un solo servidor, `docker save` / `docker load` por SSH** evita tener en el servidor una credencial para un registro.

**Alternativas descartadas.**
- **Construir en el servidor:** el servidor necesitaría el código fuente completo, el SDK de .NET y salida a NuGet. Más superficie y más tiempo con el servicio caído.
- **Etiqueta `latest`:** no dice qué versión corre, y un `pull` puede cambiarla sin aviso.
- **Un registro público:** las imágenes contienen el código compilado de la empresa.

### 6.1 Construir

En la máquina de administración:

```
source ~/mlm-docker-staging.env && cd $REPO
export VERSION=$(git rev-parse --short=7 HEAD)
export REGISTRO=mlmconqueror        # con un registro (opcion B), su direccion
SERVICIOS="signupapi adminapi billing bizcenter commissionengine rankengine \
ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups"
proyecto() { case $1 in
  signupapi) echo SignupAPI;; adminapi) echo AdminAPI;; billing) echo Billing;;
  bizcenter) echo BizCenter;; commissionengine) echo CommissionEngine;;
  rankengine) echo RankEngine;; ticketmanagementsystem) echo TicketManagementSystem;;
  sharedapicenter) echo SharedAPICenter;; adminweb) echo AdminWeb;;
  bizcenterweb) echo BizCenterWeb;; signups) echo Signups;; esac; }
for s in $SERVICIOS; do
  docker build --platform linux/amd64 --build-arg PROJECT=MLMConquerorGlobalEdition.$(proyecto $s) \
    -t $REGISTRO/$s:$VERSION . || break
done
docker images "$REGISTRO/*" --format '{{.Repository}}:{{.Tag}}' | grep -c ":$VERSION"   # 11
```

### 6.2 Opción A (un servidor): copia directa

```
docker save $(for s in $SERVICIOS; do echo $REGISTRO/$s:$VERSION; done) | gzip \
  | ssh $SERVIDOR 'gunzip | docker load'
```

**Comprobar** en el servidor: `docker images 'mlmconqueror/*'` muestra las 11 con la etiqueta `$VERSION`.

### 6.3 Opción B (varios servidores o despliegue continuo): ECR

```
export REGISTRO=$CUENTA.dkr.ecr.$AWS_REGION.amazonaws.com/mlmconqueror
for s in $SERVICIOS; do
  aws ecr describe-repositories --repository-names mlmconqueror/$s >/dev/null 2>&1 || \
  aws ecr create-repository --repository-name mlmconqueror/$s \
    --image-tag-mutability IMMUTABLE --image-scanning-configuration scanOnPush=true >/dev/null
done
aws ecr get-login-password | docker login --username AWS --password-stdin ${REGISTRO%%/*}
for s in $SERVICIOS; do
  docker tag mlmconqueror/$s:$VERSION $REGISTRO/$s:$VERSION || break
  docker push $REGISTRO/$s:$VERSION || break
done
```

En el servidor, con la sesión temporal del administrador, la descarga se hace en la Fase 8 (`docker compose pull`) después de `aws ecr get-login-password | docker login ...`, y al terminar `docker logout ${REGISTRO%%/*}`. El permiso de ECR del administrador está en la sección 2.3.

### 6.4 Archivos del despliegue en el servidor

Solo viajan los archivos versionados de `deploy/docker` y el script de migraciones, del mismo commit que las imágenes. Nunca el `.env` local ni la carpeta `deploy/docker/secrets` de la máquina de desarrollo:

```
cd $REPO
git archive $VERSION deploy/docker DeploymentScripts/migrate-rds.sql \
  | ssh $SERVIDOR 'tar -x -C /opt/mlmconqueror'
ssh $SERVIDOR 'ls /opt/mlmconqueror/deploy/docker'
```

**Comprobar:** aparecen `docker-compose.servidor.yml`, `bajar-token-tunel.sh`, `.env.servidor.ejemplo` y la carpeta `certificados` de la Fase 4.

---

## 7. Fase 6 — Base de datos

**Qué se persigue.** Crear la base, el usuario de la aplicación con la contraseña que está en el secreto `datos`, y aplicar el esquema completo antes de arrancar ningún servicio.

**Por qué así.**
- **Las migraciones se aplican una vez, antes de arrancar, con el script idempotente** `DeploymentScripts/migrate-rds.sql`, el mismo de EKS. Cada migración comprueba `__EFMigrationsHistory`, así que el script se puede repetir sin daño si corta a mitad. Los servicios llaman a `MigrateAsync()` al arrancar, pero con la base ya migrada no hacen nada: no hay 8 servicios compitiendo por migrar a la vez.
- **`mlm_app` es dueño de su base y de nada más.** La cuenta de administración del SQL Server no se guarda en ningún secreto de la aplicación.
- **La contraseña de `mlm_app` sale del secreto `datos`**: no se escribe a mano en ningún lado.
- **`sqlcmd` corre en un contenedor temporal** que se borra al terminar. No hay que instalar herramientas de SQL en el servidor.

**Alternativas descartadas.**
- **Dejar que el primer servicio que arranque cree el esquema:** si dos arrancan a la vez, chocan, y un fallo a mitad de camino deja el arranque colgado sin un registro claro.
- **Conectar la aplicación con `sa` o con la cuenta de administración:** un error de código o una inyección de SQL alcanzaría todas las bases del servidor.

En el servidor, con la sesión temporal del administrador (sección 2.5):

```
export ENTORNO=staging
DATOS=$(aws secretsmanager get-secret-value --secret-id mlmconqueror/$ENTORNO/datos \
  --query SecretString --output text)
SQL_HOST=$(jq -r .ConnectionStrings__DefaultConnection <<<"$DATOS" \
  | sed -E 's/^Server=([^,;]*).*/\1/')
APP_PASS=$(jq -r .ConnectionStrings__DefaultConnection <<<"$DATOS" \
  | sed -E 's/.*Password=([^;]*);.*/\1/')
unset DATOS
read -p "Usuario administrador del SQL Server: " SQL_ADMIN
read -s -p "Contrasena del administrador: " SQLCMDPASSWORD; echo; export SQLCMDPASSWORD
sq() { docker run --rm -i -e SQLCMDPASSWORD -v /opt/mlmconqueror/DeploymentScripts:/sql:ro \
  --entrypoint /opt/mssql-tools18/bin/sqlcmd mcr.microsoft.com/mssql/server:2022-latest \
  -S "$SQL_HOST,1433" -U "$SQL_ADMIN" -N -C -b "$@"; }
sq -Q "SELECT @@VERSION"
```

### 7.1 Crear la base y el usuario

```
sq -Q "CREATE DATABASE MLMConquerorGlobalEditionDb;"
sq -Q "ALTER DATABASE MLMConquerorGlobalEditionDb \
  SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;"
sq -Q "CREATE LOGIN mlm_app WITH PASSWORD = N'$APP_PASS', CHECK_POLICY = ON;"
sq -d MLMConquerorGlobalEditionDb -Q "CREATE USER mlm_app FOR LOGIN mlm_app; \
ALTER ROLE db_owner ADD MEMBER mlm_app;"
```

La zona horaria del sistema operativo del SQL Server tiene que ser `America/New_York` (hora del Este): el negocio usa la hora del servidor.

### 7.2 Aplicar las migraciones

Antes, en la máquina de administración, se comprueba que el script llega hasta la última migración del commit (como en la guía de EKS):

```
cd $REPO
dotnet ef migrations list --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.SignupAPI --no-connect | tail -1
```

Si esa migración no aparece en `DeploymentScripts/migrate-rds.sql`, se regenera con `dotnet ef migrations script --idempotent` (mismas opciones y `-o DeploymentScripts/migrate-rds.sql`), se hace commit y se vuelve a la Fase 5 con el commit nuevo.

En el servidor:

```
sq -d MLMConquerorGlobalEditionDb -i /sql/migrate-rds.sql > /tmp/migrate.log; echo "salida=$?"
tail -5 /tmp/migrate.log
sq -d MLMConquerorGlobalEditionDb -h -1 -Q \
  "SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;"
unset SQLCMDPASSWORD APP_PASS
```

**Comprobar:** `salida=0` y la última fila es la última migración del código. `-b` corta en el primer error; se lee `/tmp/migrate.log`, se corrige la causa y se vuelve a ejecutar.

Antes de seguir, una copia de seguridad completa de la base recién creada, con el procedimiento del propio SQL Server.

---

## 8. Fase 7 — Cloudflare Tunnel

**Qué se persigue.** Publicar los 8 nombres públicos sin abrir ningún puerto en el servidor, con el WAF de Cloudflare delante y con la IP real del cliente llegando a los servicios.

**Por qué así.**
- **El túnel es una conexión de salida.** `cloudflared` llama a Cloudflare y por esa conexión entran las peticiones. El servidor no tiene IP pública que escanear ni puerto que atacar, y un ataque de denegación de servicio se queda en Cloudflare.
- **Cada nombre apunta a un servicio por su nombre en la red `borde`** (`http://<servicio>:8080`). Billing, CommissionEngine y TicketManagementSystem no están en esa red: aunque alguien agregue por error un nombre que apunte a ellos, `cloudflared` no los encuentra.
- **El token del túnel vive en Secrets Manager** (`borde`) y en el servidor solo existe como un archivo de solo lectura. Es el único secreto en disco: sirve únicamente para hacerse pasar por el túnel y se revoca desde Cloudflare en un clic.
- **La IP del cliente es fiable.** Cloudflare agrega la IP del cliente al final de `X-Forwarded-For`; los servicios solo confían en lo que llega desde la red `borde` (`ForwardedHeaders__KnownNetwork=172.30.0.0/24`) y solo recorren un salto (`ForwardLimit` 1). Lo que el cliente escriba en esa cabecera se ignora, y un contenedor de la red `interna` no puede falsificar su IP.

**Alternativas descartadas.**
- **IP pública con firewall limitado a los rangos de Cloudflare y Authenticated Origin Pulls:** los rangos cambian y hay que mantenerlos, hay un certificado más que renovar, y el puerto sigue abierto.
- **Un proxy inverso propio (Nginx, Caddy) con Let's Encrypt:** abre el 443, y el WAF y la protección contra denegación de servicio quedan a cargo del servidor.
- **Una sola red de Docker para todo:** `cloudflared` vería a Billing y cualquier contenedor podría hacerse pasar por el proxy de confianza para falsificar IPs.
- **El token en una variable de entorno del compose:** aparece en `docker inspect` y en el `.env`.

### 8.1 Crear el túnel

En el panel de Cloudflare (Zero Trust), **Networks** (o **Networking**) → **Tunnels** → **Create a tunnel**:

1. Tipo **Cloudflared**, nombre `mlmconqueror-staging` (o `-prod`).
2. En la pantalla de instalación, elegir **Docker**. El comando que aparece termina en `--token eyJ...`. Se copia **solo el token** (lo que va después de `--token`). No se ejecuta ese comando: `cloudflared` lo arranca el compose.

### 8.2 Guardar el token en `borde`

En la máquina de administración:

```
source ~/mlm-docker-staging.env
umask 077; set +o history
read -s -p "Token del tunel: " T; echo
jq -n --arg t "$T" '{"Cloudflare__TunnelToken": $t}' > /tmp/borde.json; unset T
bash $REPO/deploy/aws/cargar-secreto.sh $ENTORNO borde /tmp/borde.json && shred -u /tmp/borde.json
set -o history
```

**Comprobar:** `cargado: mlmconqueror/staging/borde (1 claves). Lo leen:` y ningún servicio debajo.

### 8.3 Nombres públicos

En el túnel → **Public Hostname** (en el panel nuevo, **Published application routes**) → **Add**, uno por fila. Tipo de servicio **HTTP** en todos:

| Subdominio | Dominio | Ruta | URL |
|---|---|---|---|
| `admin` | `${DOMINIO}` | (vacía) | `adminweb:8080` |
| `app` | `${DOMINIO}` | (vacía) | `bizcenterweb:8080` |
| `signup` | `${DOMINIO}` | (vacía) | `signups:8080` |
| `auth-api` | `${DOMINIO}` | (vacía) | `signupapi:8080` |
| `admin-api` | `${DOMINIO}` | (vacía) | `adminapi:8080` |
| `api` | `${DOMINIO}` | (vacía) | `bizcenter:8080` |
| `rank` | `${DOMINIO}` | `^/certificates` | `rankengine:8080` |
| `integrations` | `${DOMINIO}` | `^/api/v1/external` | `sharedapicenter:8080` |

La ruta es una expresión regular. En `rank` e `integrations`, cualquier otra ruta no coincide con ninguna regla y Cloudflare responde 404 sin llegar al servidor. Cloudflare crea solo el registro DNS de cada nombre, con el proxy activado.

### 8.4 Reglas de WAF

En la zona `${DOMINIO}` → **Security** → **WAF** → **Custom rules** → **Create rule**, nombre `bloquear-rutas-internas`, acción **Block**, con esta expresión (**Edit expression**):

```
starts_with(lower(http.request.uri.path), "/swagger")
or starts_with(lower(http.request.uri.path), "/hangfire")
or starts_with(lower(http.request.uri.path), "/api/v1/dev")
or starts_with(lower(http.request.uri.path), "/api/v1/webhooks/")
```

Las rutas de webhooks de SharedAPICenter no comprueban firma: por eso no se publican. Los paneles de Hangfire, además, solo aceptan peticiones desde el propio contenedor.

### 8.5 TLS y WebSockets

En la zona `${DOMINIO}`:

- **SSL/TLS** → **Overview**: modo **Full**.
- **SSL/TLS** → **Edge Certificates**: **Always Use HTTPS** activado y **Minimum TLS Version** 1.2.
- **Network**: **WebSockets** activado. Los portales son Blazor Server y sin WebSockets no funcionan.

### 8.6 Bajar el token en el servidor

En el servidor, con la sesión temporal del administrador:

```
cd /opt/mlmconqueror
bash deploy/docker/bajar-token-tunel.sh staging
ls -l deploy/docker/secretos/tunel-token
```

**Comprobar:** `Token del tunel escrito en deploy/docker/secretos/tunel-token`, con permisos `-r--r--r--` (el contenedor `cloudflared` corre con otro usuario y tiene que leerlo). La carpeta `secretos` tiene permisos 700: ningún otro usuario del servidor entra en ella. Si dice `El secreto borde no tiene token`, falta la sección 8.2.

---

## 9. Fase 8 — `.env.servidor` y arranque

**Qué se persigue.** Dar al compose los valores no secretos del entorno y arrancar los 12 contenedores en orden.

**Por qué así.**
- **`.env.servidor` solo tiene datos que no son secretos:** la cuenta, los ARN de Roles Anywhere, la versión, la llave pública JWT y las URL públicas. Si se filtra, no da acceso a nada.
- **Un archivo de configuración de AWS por servicio**, generado por el compose con su rol (`MLM-${ENTORNO}-<servicio>`), montado en `/run/aws/config`. Un servicio no puede pedir el rol de otro aunque se cambie su configuración, porque su certificado no lo permite.
- **Arranque en cadena por salud:** signupapi primero (siembra la cuenta raíz), después adminapi, después el resto. Ningún servicio arranca contra otro que todavía no está sano.

**Alternativas descartadas.**
- **Escribir los ARN y la versión en el compose:** cada entorno necesitaría su propio compose y se desalinearían con el tiempo.
- **Arrancar todo a la vez:** varios servicios fallarían al no encontrar a los que llaman y entrarían en reinicios hasta que el resto estuviera listo.

### 9.1 `.env.servidor`

En el servidor:

```
cd /opt/mlmconqueror/deploy/docker
cp .env.servidor.ejemplo .env.servidor && chmod 600 .env.servidor
nano .env.servidor
```

| Variable | Valor |
|---|---|
| `ENTORNO` | `staging` (o `prod`) |
| `AWS_REGION` | `us-east-1` |
| `CUENTA` | La cuenta de AWS del entorno |
| `TRUST_ANCHOR_ARN` y `PROFILE_ARN` | Las dos líneas que imprimió `roles-anywhere.sh` (Fase 3) |
| `REGISTRO` | `mlmconqueror` con la opción A de la Fase 5; la dirección de ECR con la B |
| `VERSION` | El commit corto de la Fase 5 |
| `JWT_PUBLIC` | El contenido de `deploy/aws/llaves-publicas/${ENTORNO}-jwt-publica.b64` |
| `URL_ADMIN` | `https://admin.${DOMINIO}` |
| `URL_APP` | `https://app.${DOMINIO}` |
| `URL_SIGNUP` | `https://signup.${DOMINIO}` |
| `URL_AUTH_API` | `https://auth-api.${DOMINIO}` |
| `URL_CERTIFICADOS` | `https://rank.${DOMINIO}` |
| `URL_RECIBOS` | `https://admin-api.${DOMINIO}` |

Los ARN se pueden volver a ver desde la máquina de administración con `bash deploy/aws/roles-anywhere.sh $ENTORNO` (no crea nada si ya existen).

### 9.2 Arrancar

```
cd /opt/mlmconqueror
dc() { docker compose -f deploy/docker/docker-compose.servidor.yml \
  --env-file deploy/docker/.env.servidor "$@"; }
dc config --quiet && echo "compose valido"
dc up -d
dc ps
```

Con la opción B de la Fase 5, antes de `dc up -d`: `aws ecr get-login-password | docker login --username AWS --password-stdin ${REGISTRO%%/*}`, `dc pull` y `docker logout ${REGISTRO%%/*}` (con `REGISTRO` tomado de `.env.servidor`).

`dc` se vuelve a definir en cada sesión. `dc config --quiet` falla con `falta ENTORNO en .env.servidor` o `falta JWT_PUBLIC en .env.servidor` si quedó algo sin llenar.

El arranque completo tarda unos minutos (el compose da hasta 10 minutos a cada servicio para estar sano).

**Comprobar:** `dc ps` muestra los 11 servicios `healthy` y `cloudflared` `running`. En Cloudflare, el túnel aparece **Healthy**.

### 9.3 Las líneas `[SecretosAws]`

```
for s in signupapi adminapi billing bizcenter commissionengine rankengine \
  ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups; do
  echo "== $s"; dc logs --no-log-prefix $s 2>&1 | grep "\[SecretosAws\]"
done
```

Lo esperado (con `ENTORNO=staging`):

| Servicio | Líneas |
|---|---|
| signupapi | `datos: 3`, `jwt-firma: 1`, `signupapi: 4`, `firebase: 1`, `twilio: 2` |
| adminapi | `datos: 3`, `jwt-firma: 1`, `dp-gateway: 2`, `twilio: 2` |
| billing | `datos: 3`, `dp-gateway: 2`, `billing: 1`, `firebase: 1`, `twilio: 2` |
| bizcenter | `datos: 3`, `dp-gateway: 2`, `firebase: 1`, `twilio: 2` |
| commissionengine y ticketmanagementsystem | `datos: 3`, `firebase: 1` |
| rankengine | `datos: 3`, `firebase: 1`, `twilio: 2` |
| sharedapicenter | `datos: 3`, `sharedapicenter: 1`, `firebase: 1` |
| adminweb y bizcenterweb | `portales: 1` |
| signups | ninguna |

`dp-gateway` muestra más de 2 claves si hay certificados retirados.

Si un servicio no queda sano:

```
dc logs --tail=200 <servicio>
```

| Síntoma en el log | Causa |
|---|---|
| `[SecretosAws] ...` | La tabla de la sección 1.5 |
| `Detalle: ... permission denied` (sobre `/run/aws/key.pem`) | Permisos o dueño del certificado: repetir la sección 5.2 |
| `Detalle: ... AccessDeniedException` de Roles Anywhere | Certificado de otro servicio, de otro entorno, vencido o revocado; o el rol no está en el perfil (volver a correr `roles-anywhere.sh`) |
| `Detalle: ... Signing time` o `expired` | Reloj del servidor desfasado (sección 2.5) |
| `A network-related` | El servidor no llega al SQL Server |
| `Cache:Mode=Required` | El servidor no llega al Redis |
| `is not configured` | Falta una clave en un secreto: revisar el JSON que se cargó |

---

## 10. Fase 9 — Comprobaciones

**Qué se persigue.** Comprobar desde fuera que todo funciona y que cada barrera de seguridad hace su trabajo: los secretos solo llegan a quien deben, la IP del cliente es la real y el servidor no tiene puertas abiertas.

**Por qué así.** Una barrera que no se ha probado no se sabe si existe. Cada comprobación de esta fase intenta saltarse una de las decisiones de la guía y tiene que fallar.

**Alternativas descartadas.** Dar por buena la instalación porque los servicios están `healthy`: un servicio sano puede estar leyendo secretos que no le tocan o registrando la IP de Cloudflare en lugar de la del cliente.

### 10.1 Salud pública

Desde la máquina de administración:

```
for h in admin app signup auth-api admin-api api; do
  printf "%-12s" $h; curl -s -o /dev/null -w "%{http_code}\n" https://$h.$DOMINIO/health; done
for u in auth-api.$DOMINIO/swagger/index.html api.$DOMINIO/hangfire \
  integrations.$DOMINIO/api/v1/webhooks/x rank.$DOMINIO/health; do
  printf "%-45s" $u; curl -s -o /dev/null -w "%{http_code}\n" https://$u; done
```

**Comprobar:** los 6 primeros dan `200`. Los 4 siguientes dan `403` (bloqueado por el WAF) o `404` (sin regla en el túnel).

### 10.2 Inicio de sesión, token aceptado e IP real del cliente

Se inicia sesión con la cuenta raíz contra `auth-api`, se cambia su contraseña (lo que además cumple con cambiarla en el primer inicio de sesión) y se lee el registro de cambios de credenciales, que guarda la IP desde la que se hizo. La petición lleva además una cabecera `X-Forwarded-For` falsa, que no debe tener ningún efecto.

```
MI_IP=$(curl -s https://www.cloudflare.com/cdn-cgi/trace | sed -n 's/^ip=//p'); echo $MI_IP
set +o history
read -p "Correo de la cuenta raiz: " ROOT_MAIL
read -s -p "Contrasena actual: " ROOT_PASS; echo
NUEVA="$(openssl rand -base64 18 | tr -d '/+=')Aa1#"
TOKEN=$(curl -s https://auth-api.$DOMINIO/api/v1/auth/login -H 'Content-Type: application/json' \
  -d "$(jq -n --arg e "$ROOT_MAIL" --arg p "$ROOT_PASS" '{email:$e,password:$p}')" \
  | jq -r '.data.accessToken // empty')
[ -n "$TOKEN" ] && echo "login correcto" || echo "login fallido"
curl -s -X PUT https://auth-api.$DOMINIO/api/v1/auth/change-password \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -H 'X-Forwarded-For: 1.2.3.4' \
  -d "$(jq -n --arg a "$ROOT_PASS" --arg n "$NUEVA" '{currentPassword:$a,newPassword:$n}')" \
  | jq -r .success
echo "Nueva contrasena de la cuenta raiz (al gestor de contrasenas): $NUEVA"
curl -s -o /dev/null -w "bizcenter acepta el token: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN" https://api.$DOMINIO/api/v1/bizcenter/profile
curl -s -H "Authorization: Bearer $TOKEN" \
  https://api.$DOMINIO/api/v1/bizcenter/profile/credentials-history \
  | jq -r '[.. | objects | select(has("ipAddress")) | .ipAddress][0]'
unset TOKEN ROOT_PASS NUEVA ROOT_MAIL; set -o history
```

**Comprobar:**
- `login correcto` y `true` en el cambio de contraseña: SignupAPI firmó un token y lo aceptó.
- `bizcenter acepta el token: 200`: otro servicio valida el token con la llave pública de `.env.servidor`.
- La última línea es igual a `MI_IP`. No es `1.2.3.4` (la cabecera falsa se ignoró) ni una IP de Cloudflare ni de `172.30.0.0/24`.

Si el login devuelve `requiresTwoFactor`, la cuenta tiene el 2FA encendido y el token sale de `/api/v1/auth/two-factor/verify`; para esta prueba basta con hacerlo desde el portal.

### 10.3 Aislamiento: el certificado de billing no alcanza lo ajeno

En el servidor, con la sesión del administrador (solo para el AWS CLI; las credenciales que se prueban son las del certificado):

```
cd /opt/mlmconqueror && set -a && . deploy/docker/.env.servidor && set +a
rol() { dc exec -T billing /usr/local/bin/aws_signing_helper credential-process \
  --certificate /run/aws/cert.pem --private-key /run/aws/key.pem \
  --trust-anchor-arn "$TRUST_ANCHOR_ARN" --profile-arn "$PROFILE_ARN" \
  --role-arn "arn:aws:iam::$CUENTA:role/MLM-$ENTORNO-$1"; }
rol signupapi; echo "salida=$?"
```

**Comprobar:** error de acceso denegado y `salida=` distinto de 0. El certificado de billing no puede asumir el rol de signupapi.

```
C=$(rol billing)
como_billing() { AWS_ACCESS_KEY_ID=$(jq -r .AccessKeyId <<<"$C") \
  AWS_SECRET_ACCESS_KEY=$(jq -r .SecretAccessKey <<<"$C") \
  AWS_SESSION_TOKEN=$(jq -r .SessionToken <<<"$C") env -u AWS_PROFILE "$@"; }
como_billing aws sts get-caller-identity --query Arn --output text
como_billing aws secretsmanager get-secret-value --secret-id mlmconqueror/$ENTORNO/datos \
  --query Name --output text
como_billing aws secretsmanager get-secret-value --secret-id mlmconqueror/$ENTORNO/jwt-firma \
  --query Name --output text
unset C
```

**Comprobar:**
- La primera muestra `assumed-role/MLM-staging-billing/...`.
- La segunda muestra el nombre de `datos` (Billing lo lee; `--query Name` no imprime el valor).
- La tercera falla con `AccessDeniedException`: Billing no puede leer la llave de firma JWT.

### 10.4 Redes y puertos

En el servidor:

```
redes() { docker inspect $(dc ps -q $1) --format '{{json .NetworkSettings.Networks}}' \
  | jq -r 'keys[]'; }
redes billing
redes cloudflared
sudo ss -tlnp
```

**Comprobar:**
- Billing solo está en `mlmconqueror_interna`; `cloudflared` solo en `mlmconqueror_borde`.
- `ss -tlnp` no muestra `docker-proxy` ni el 8080: solo SSH y lo propio del sistema. Desde fuera, el `nc` de la sección 2.5 sigue fallando.

### 10.5 En el navegador

1. `https://signup.${DOMINIO}`: carga el alta, y en las herramientas de desarrollo las llamadas van a `auth-api`.
2. `https://admin.${DOMINIO}`: iniciar sesión con la cuenta raíz y la contraseña nueva.
3. Con la sesión abierta en `admin`, en el servidor: `dc restart adminweb`. Al volver a estar sano, la sesión sigue abierta (las llaves de Data Protection están en un volumen).
4. En Admin, pasarelas de pago: cargar las credenciales del entorno. Quedan cifradas con el PFX de `dp-gateway`.
5. Los portales no muestran el aviso de licencia de Syncfusion.

Al terminar, en el servidor: `aws sso logout`.

---

## 11. Operación

**Qué se persigue.** Que los cambios del día a día (un secreto nuevo, una versión nueva, certificados que vencen, un certificado o un token robado, una caída de AWS) tengan un procedimiento corto y probado.

**Por qué así.**
- **Los servicios leen los secretos solo al arrancar.** Cambiar un secreto es cargarlo y reiniciar los servicios que lo leen: no hay estados a medias ni conexiones que se recargan solas.
- **Revocar es inmediato y no toca los demás servicios:** una lista de revocación en Roles Anywhere invalida un certificado concreto; un token de túnel se invalida en Cloudflare.

**Alternativas descartadas.**
- **Recarga de secretos en caliente:** obligaría a que las conexiones de SQL y Redis y las llaves ya cargadas supieran recargarse, y un fallo a mitad dejaría un servicio con una mezcla de valores viejos y nuevos.
- **Desactivar el trust anchor para revocar un certificado:** funciona, pero deja sin identidad a los 10 servicios. Queda solo para el caso de que se comprometa la autoridad misma.

### 11.1 Cambiar un secreto

En la máquina de administración, desde una carpeta temporal como la de la sección 3.2:

```
bash $REPO/deploy/aws/cargar-secreto.sh $ENTORNO <secreto> <archivo.json> && shred -u <archivo.json>
jq -r --arg x <secreto> '.servicios | to_entries[] | select(.value.secretos | index($x))
  | .key | ascii_downcase' $REPO/deploy/aws/secretos-por-servicio.json | paste -sd' '
```

En el servidor, con los nombres que imprimió el segundo comando:

```
dc restart <servicios>
dc logs --since 5m <servicio> | grep "\[SecretosAws\]"
```

`cargar-secreto.sh` reemplaza el JSON entero: el archivo lleva todas las claves del secreto, no solo la que cambia.

Casos especiales:
- **`jwt-firma`:** se cambian juntas la privada y `JWT_PUBLIC` en `.env.servidor`, siguiendo `docs/deployment/rotacion-llaves-jwt.md`. Como cambia `.env.servidor`, no basta `restart`: `dc up -d` recrea los contenedores. Todas las sesiones se cierran.
- **`dp-gateway`:** el PFX anterior pasa a `DataProtection__Certificate__Retired__0__Base64` y `__Password` en el mismo JSON, para que lo ya cifrado se siga leyendo.
- **`datos`:** si cambia la contraseña de `mlm_app`, primero se cambia en el SQL Server (`ALTER LOGIN mlm_app WITH PASSWORD = ...`) y justo después se carga el secreto y se reinician los 8 servicios.

### 11.2 Desplegar una versión nueva

Fase 5 con el commit nuevo (y la sección 7.2 si trae migraciones, después de una copia de la base). En el servidor, `VERSION` nueva en `.env.servidor` y:

```
dc up -d
dc ps
```

Volver atrás es poner la `VERSION` anterior y repetir `dc up -d` (si la versión nueva no migró la base).

### 11.3 Renovar los certificados

Los certificados valen un año. Al emitirlos se crea un recordatorio en el calendario del equipo **a los 11 meses**. La fecha de vencimiento se ve en el servidor:

```
sudo openssl x509 -enddate -noout -in /opt/mlmconqueror/deploy/docker/certificados/billing/cert.pem
```

Para renovar: traer `deploy/aws/ca/${ENTORNO}/` a la máquina de administración, repetir las secciones 5.1, 5.2 y 5.3 y, en el servidor, `dc up -d --force-recreate`. Los certificados anteriores siguen valiendo hasta su fecha; si no hay motivo para revocarlos, se dejan vencer. Después, `sudo rm -rf certificados.anterior` en el servidor y la autoridad vuelve a su sitio fuera de la máquina.

### 11.4 Revocar un certificado robado

En la máquina de administración, con `deploy/aws/ca/${ENTORNO}/` traída de vuelta. La primera vez se crea la base de datos de la autoridad:

```
cd $REPO/deploy/aws/ca/$ENTORNO
[ -f ca.cnf ] || cat > ca.cnf <<'EOF'
[ ca ]
default_ca = mlm

[ mlm ]
dir              = .
database         = $dir/index.txt
crlnumber        = $dir/crlnumber
certificate      = $dir/ca.crt
private_key      = $dir/ca.key
default_md       = sha256
default_crl_days = 365
EOF
touch index.txt; [ -f crlnumber ] || echo 1000 > crlnumber
```

Revocar el certificado (el archivado en `emitidos/` en la sección 5.3) y generar la lista:

```
openssl ca -config ca.cnf -revoke emitidos/billing-20261120.pem -crl_reason keyCompromise
openssl ca -config ca.cnf -gencrl -out crl.pem
openssl crl -in crl.pem -noout -text | grep -A1 "Serial Number"
```

Importarla en Roles Anywhere (formato PEM) y retirar las listas anteriores, porque la nueva ya las incluye:

```
ANCLA=$(aws rolesanywhere list-trust-anchors \
  --query "trustAnchors[?name=='mlmconqueror-$ENTORNO'].trustAnchorArn | [0]" --output text)
VIEJAS=$(aws rolesanywhere list-crls \
  --query "crls[?trustAnchorArn=='$ANCLA'].crlId" --output text)
aws rolesanywhere import-crl --name "mlmconqueror-$ENTORNO-$(date +%Y%m%d%H%M)" \
  --crl-data fileb://crl.pem --trust-anchor-arn $ANCLA --enabled --query crl.crlId
for id in $VIEJAS; do aws rolesanywhere delete-crl --crl-id $id; done
```

Desde ese momento, el certificado revocado no obtiene credenciales nuevas. Las que ya obtuvo vencen en menos de una hora. Después se emite uno nuevo solo para ese servicio y se reinicia:

```
cd $REPO && bash deploy/aws/emitir-certificados.sh $ENTORNO Billing
```

y se copian, se fijan permisos y se archivan como en las secciones 5.2 y 5.3, pero solo para esa carpeta (en el servidor, `sudo rm -rf certificados/billing` antes de mover la nueva). Por último, `dc up -d --force-recreate billing`.

`emitir-certificados.sh` recibe el nombre del servicio como está en `secretos-por-servicio.json` (`Billing`, `SignupAPI`...). Roles Anywhere no mira la fecha de vencimiento de una lista importada: no hace falta volver a importarla cada año, solo cada vez que se revoca algo.

Si lo comprometido es la autoridad misma (`ca.key`): `aws rolesanywhere disable-trust-anchor`, borrar la carpeta de la autoridad, crear una nueva con `roles-anywhere.sh` (un trust anchor nuevo), volver a correr `crear-roles.sh $ENTORNO docker` y `roles-anywhere.sh`, emitir los 10 certificados y actualizar `TRUST_ANCHOR_ARN` en `.env.servidor`.

### 11.5 Rotar el token del túnel

Una vez al año, o de inmediato si el servidor o el token se comprometen:

1. En Cloudflare: **Networks** → **Tunnels** → el túnel → **Refresh token**. Se copia el token nuevo del comando de instalación. Los conectores que ya están abiertos siguen funcionando; el token viejo ya no abre conexiones nuevas.
2. En la máquina de administración, la sección 8.2 con el token nuevo.
3. En el servidor, con la sesión del administrador:

```
cd /opt/mlmconqueror
bash deploy/docker/bajar-token-tunel.sh $ENTORNO
dc restart cloudflared
dc logs --since 2m cloudflared | grep -i "registered tunnel connection"
```

Si el token se robó, además se cortan las conexiones abiertas del túnel (en el panel, o con la API de Cloudflare borrando las conexiones del túnel) antes del paso 3.

### 11.6 Si AWS no responde

- **Los servicios que ya están corriendo siguen funcionando:** leyeron sus secretos al arrancar y los tienen en memoria. Solo fallan las llamadas a S3 o SES, si el entorno las usa.
- **Un servicio que se reinicia no arranca** hasta que AWS vuelva. Docker lo reintenta solo (`restart: unless-stopped`) y arranca en cuanto AWS responda; no hay que hacer nada.
- **Mientras dure la caída:** no se despliega, no se reinicia nada a mano y no se reinicia el servidor.
- **Último recurso**, solo con decisión explícita del responsable: `SecretosAws__Habilitado: "false"` en el servicio y sus valores como variables de entorno en el compose. Deja los secretos en texto plano en el servidor: se revierte en cuanto vuelva AWS y se cambian los secretos expuestos.

---

## 12. Tu máquina (entorno `local`)

**Qué se persigue.** Que cada desarrollador corra los servicios con los secretos de desarrollo (`mlmconqueror/local/*`) sin copiarlos a archivos, y que pueda trabajar sin conexión cuando haga falta.

**Por qué así.**
- **El mismo mecanismo que en staging y prod.** El código que lee los secretos se prueba todos los días en la máquina del desarrollador; un error en la lista de un servicio se ve en el primer `dotnet run`, no en el despliegue.
- **El desarrollador lee todo `local` y nada más.** Su permiso no alcanza `staging` ni `prod`, y `local` no tiene ningún valor real: base local, llaves de desarrollo.
- **Sin archivos con secretos en el disco del desarrollador.** Un portátil perdido no lleva la licencia de Syncfusion ni la llave de firma de nadie.

**Alternativas descartadas.**
- **Secretos de desarrollo en `appsettings.Development.json` subido al repositorio:** cualquiera con el repositorio los tiene, y es como se filtró la llave JWT en el pasado.
- **Cada desarrollador con su propio archivo de secretos:** se desalinean, y un valor de producción acaba copiado en un portátil.

### 12.1 Permiso del desarrollador

Lo aplica una vez el administrador de la cuenta de desarrollo, para el grupo de IAM de los desarrolladores:

```
bash deploy/aws/politica-desarrollador-local.sh local <grupo-iam>
```

El grupo puede leer y descifrar `mlmconqueror/local/*` y nada de `staging` ni de `prod`. Con IAM Identity Center, se pega el mismo permiso en el *permission set* de desarrollo.

Los secretos `local` se crean como los de la Fase 2, con `ENTORNO=local` y los valores de la máquina de desarrollo (base `MLMConquerorGlobalEditionDb` local, Redis local, llave JWT y PFX de desarrollo). `borde` queda vacío en `local`. En la cuenta de desarrollo ya están creados.

### 12.2 Perfil de AWS

```
aws configure sso --profile mlm-local      # una vez; o claves en ~/.aws con aws configure
aws sso login --profile mlm-local
export AWS_PROFILE=mlm-local
aws secretsmanager describe-secret --secret-id mlmconqueror/local/datos --query Name
```

En PowerShell: `$env:AWS_PROFILE = 'mlm-local'`. Si el perfil es el `default`, no hace falta la variable.

### 12.3 `dotnet run`

```
dotnet run --project MLMConquerorGlobalEdition.SignupAPI
```

**Comprobar:** antes del resto del arranque aparecen

```
[SecretosAws] mlmconqueror/local/datos: 3 clave(s).
[SecretosAws] mlmconqueror/local/jwt-firma: 1 clave(s).
[SecretosAws] mlmconqueror/local/signupapi: 4 clave(s).
[SecretosAws] mlmconqueror/local/firebase: 1 clave(s).
[SecretosAws] mlmconqueror/local/twilio: 2 clave(s).
```

Si la sesión de SSO venció, el servicio no arranca y dice `Este servicio no tiene identidad de AWS`: `aws sso login` y otra vez `dotnet run`.

### 12.4 El docker-compose local

`deploy/docker/docker-compose.yml` levanta los 11 servicios con SQL Server y Redis propios del compose, con la misma imagen que va al servidor. Los servicios leen `mlmconqueror/local/*` con el perfil de AWS de quien lo levanta, montado de solo lectura (`~/.aws` en `/home/app/.aws`; en Windows, `%USERPROFILE%\.aws`).

```
bash deploy/docker/generar-secretos.sh            # una vez: contrasena del SQL Server de prueba
AWS_PROFILE=mlm-local docker compose -f deploy/docker/docker-compose.yml up -d --build
docker compose -f deploy/docker/docker-compose.yml ps
```

- La cadena de SQL y la de Redis del compose apuntan al SQL Server y al Redis del propio compose. Van como variables de entorno y **ganan** sobre las del secreto `datos`, que apuntan a la base de la máquina.
- El resto (llave JWT, PFX, cuenta raíz, licencias) llega de `mlmconqueror/local/*`.
- Si la carpeta del perfil no está en el sitio habitual: `AWS_CONFIG_DIR=/ruta/a/.aws`.
- `deploy/docker/.env` solo tiene la contraseña `sa` del SQL Server del compose, que es local y desechable.

### 12.5 Trabajar sin conexión

En el `appsettings.Development.json` del servicio (que no se sube al repositorio):

```
"SecretosAws": { "Habilitado": false }
```

El servicio escribe `[SecretosAws] Deshabilitado: se usan solo appsettings y variables de entorno.` y no llama a AWS. Los valores tienen que estar entonces en ese mismo archivo. Al volver a tener conexión se quita la línea, para que el servicio vuelva a probar la lectura real.

---

## 13. Lista final de comprobación

| # | Comprobación | Cómo |
|---|---|---|
| 1 | Llave KMS del entorno con rotación anual | Sección 3.1 |
| 2 | Los 10 secretos del entorno cargados (`borde` incluido) | Secciones 3.13 y 8.2 |
| 3 | PFX y contraseña de `dp-gateway`, y contraseña de la cuenta raíz, guardados fuera de AWS | Secciones 3.5, 3.6 y 10.2 |
| 4 | La autoridad certificadora no está en el servidor ni en la máquina de administración, sino guardada fuera | Secciones 4 y 5.3 |
| 5 | Certificados en el servidor con permisos 600 y dueño 1654; llaves borradas de la máquina de administración | Secciones 5.2 y 5.3 |
| 6 | Recordatorio de renovación de certificados a los 11 meses | Sección 11.3 |
| 7 | Los 11 servicios `healthy` y el túnel `Healthy` en Cloudflare | Sección 9.2 |
| 8 | Cada servicio cargó exactamente sus secretos | Sección 9.3 |
| 9 | Salud pública 200; Swagger, Hangfire, `/api/v1/dev` y webhooks bloqueados desde fuera | Sección 10.1 |
| 10 | Login correcto y token aceptado por otro servicio | Sección 10.2 |
| 11 | El registro de credenciales trae la IP real del cliente, no la de Cloudflare ni la de una cabecera falsa | Sección 10.2 |
| 12 | El certificado de billing no asume el rol de signupapi ni lee `jwt-firma` | Sección 10.3 |
| 13 | Billing no es alcanzable por el túnel (solo en la red `interna`) | Sección 10.4 |
| 14 | El servidor no tiene puertos de entrada: `ss -tlnp` solo muestra lo local y el `nc` desde fuera falla | Secciones 2.5 y 10.4 |
| 15 | Reloj del servidor sincronizado por NTP | Sección 2.5 |
| 16 | `.env.servidor` con permisos 600 y sin ningún secreto | Sección 9.1 |
| 17 | Sesión del administrador cerrada en el servidor (`aws sso logout`) | Sección 10.5 |
| 18 | Copia de seguridad de la base tras el esquema | Sección 7.2 |
