# Despliegue en AWS EKS — Guía paso a paso

**Fecha:** 19-nov-2026
**Código de referencia:** `master` en `17197f4`
**Alcance:** los 11 servicios de MLM Conqueror Global Edition en Amazon EKS, con AWS Secrets Manager, RDS for SQL Server, ElastiCache, EFS, S3, SES, ECR, ACM, Route 53, WAF, CloudWatch y GitHub Actions.
**Fuera del alcance:** la carga de datos del sistema anterior y la publicación de las apps MAUI en las tiendas (la sección 15 dice qué cambiar en ellas).

---

## Cómo usar esta guía

- Las fases van en orden y cada una depende de las anteriores. No se salta ninguna.
- Cada paso dice qué hacer, el comando exacto y cómo comprobar que quedó bien. No se pasa al siguiente paso hasta que la comprobación da lo esperado.
- Todo se ejecuta desde una terminal **Linux** (WSL2 con Ubuntu 24.04 en la máquina Windows). Los comandos son bash; no se ejecutan en PowerShell.
- Los valores que cambian entre instalaciones viven en variables de entorno, en un archivo que se crea en la Fase 1. Los manifiestos de Kubernetes usan `${VARIABLE}` y se aplican con la función `aplicar`, que sustituye esas variables antes de mandarlos al clúster.
- Ningún secreto se escribe en el repositorio, en un manifiesto ni en una variable que quede en el historial de la terminal sin borrar. Los secretos van de un archivo temporal a Secrets Manager y el archivo se borra en el mismo paso.
- Esta guía no toca la instancia RDS de producción del sistema anterior. La base del sistema nuevo es una instancia nueva.

## Decisiones que hay que tener antes de empezar

| Decisión | Valor que asume la guía | Dónde se usa |
|---|---|---|
| Dominio | `mlmconqueror.com` (confirmado por Nick el 19-nov-2026). En DNS no importan las mayúsculas: `MLMConqueror.com` y `mlmconqueror.com` son el mismo dominio. | DNS, certificado, CORS, enlaces de correo |
| Región de AWS | `us-east-1` | Todo; SES y S3 ya usan esa región en appsettings |
| Zona horaria del negocio | `America/New_York` (Windows: `Eastern Standard Time`), confirmada por Nick el 19-nov-2026. Es la hora del Este con cambio de horario: UTC-4 en verano y UTC-5 en invierno. | `TZ` de los pods y zona de RDS. El código usa la hora del servidor para el negocio. En RDS no se puede cambiar después de crear la instancia. |
| Proyecto de Firebase para push | Sí, con el JSON de la cuenta de servicio | 7 servicios. Sin él, push queda apagado y el resto funciona. |
| Licencia de Syncfusion | Clave de licencia disponible | AdminWeb y BizCenterWeb |
| Entrax (pago en efectivo por QR) | Apagado en el primer despliegue y encendido en la Fase 14 | SignupAPI |
| Cuenta de Stripe | No se usa (queda el valor de prueba) | Billing |
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

Billing, CommissionEngine y TicketManagementSystem no tienen nombre público: solo se llega a ellos desde dentro del clúster.

---

## 1. Arquitectura

- **Red:** una VPC nueva (`10.20.0.0/16`) en tres zonas de disponibilidad, con subredes públicas para el balanceador y privadas para los nodos, la base y Redis. Tiene un NAT Gateway por zona.
- **Cómputo:** un clúster EKS con un grupo de nodos administrado de 3 a 6 nodos `m6i.xlarge` en las subredes privadas.
- **Entrada:** un único Application Load Balancer (ALB), creado por el AWS Load Balancer Controller. Termina TLS con un certificado de ACM, protege con AWS WAF y enruta por nombre de host. Los portales Blazor llevan afinidad de sesión (stickiness).
- **Dentro del clúster:**
  - Cada servicio escucha HTTP en el puerto 8080.
  - Los servicios se llaman entre sí por HTTP interno: `http://<servicio>.mlmconqueror.svc.cluster.local`.
- **Datos:**
  - RDS for SQL Server Standard en Multi-AZ, con una sola base `MLMConquerorGlobalEditionDb`. La comparten los 8 servicios con base y Hangfire.
  - ElastiCache Redis con TLS y token.
  - S3 para archivos.
  - EFS para lo que los servicios escriben en disco: adjuntos, recibos de pago, certificados PDF y llaves de Data Protection.
- **Secretos:**
  - Viven en AWS Secrets Manager, cifrados con una llave KMS propia.
  - External Secrets Operator los copia a Secrets de Kubernetes y los pods los reciben como variables de entorno o como archivos montados.
- **Permisos de AWS de los pods:** IRSA (IAM Roles for Service Accounts). No hay llaves de acceso de AWS en la configuración.
- **Imágenes:** Amazon ECR, un repositorio por servicio.
- **Observabilidad:** CloudWatch Container Insights recoge métricas y logs, y las alarmas avisan por SNS.

| Servicio | Proyecto | Base de datos | Redis | Hangfire (cola) | Disco en EFS | AWS (IRSA) |
|---|---|---|---|---|---|---|
| signupapi | SignupAPI | Sí | Sí | `signups` | Llaves DP | S3, SES |
| adminapi | AdminAPI | Sí | Sí | No | Adjuntos, recibos | S3, SES |
| billing | Billing | Sí | Sí | 8 colas `billing*` | Recibos | SES |
| bizcenter | BizCenter | Sí | Sí | `bizcenter` | Adjuntos | S3, SES |
| commissionengine | CommissionEngine | Sí | Sí | `commissions` | No | No |
| rankengine | RankEngine | Sí | Sí | `rank` | Certificados | S3, SES |
| ticketmanagementsystem | TicketManagementSystem | Sí | Sí | `tickets` | No | No |
| sharedapicenter | SharedAPICenter | Sí | Sí | No | No | No |
| adminweb | AdminWeb | No | No | No | Llaves DP | No |
| bizcenterweb | BizCenterWeb | No | No | No | Llaves DP | No |
| signups | Signups | No | No | No | Llaves DP | No |

Las migraciones de la base se aplican **una vez, antes de arrancar los servicios**, con el script idempotente `DeploymentScripts/migrate-rds.sql`. Los servicios también llaman a `MigrateAsync()` al arrancar; con la base ya migrada no hacen nada.

---

## 2. Fase 0 — Cambios de código obligatorios antes de construir las imágenes

El código de `17197f4` estaba hecho para correr directamente en Kestrel en una máquina. Detrás de un balanceador, con varias réplicas y en Linux, había siete cosas que fallaban o quedaban inseguras. **Los siete cambios están hechos en la rama `fase0-eks`.** Esta sección dice qué se cambió y dónde, para que se pueda revisar. Las imágenes se construyen solo desde un `master` que ya los tenga integrados (sección 2.6).

| # | Problema | Cambio | Dónde |
|---|---|---|---|
| 1 | No había cómo construir imágenes | `Dockerfile` único para los 11 servicios y `.dockerignore` | Raíz del repositorio |
| 2 | Detrás del ALB los servicios veían la IP del balanceador y `http`; el límite por IP se podía falsear con `X-Real-IP` | `AddProxyForwardedHeaders` y `app.UseForwardedHeaders()` como primer middleware en los 11 `Program.cs` | `SharedKernel.Server/Hosting/ProxyHosting.cs` |
| 2b | SignupAPI tomaba la primera IP de `X-Forwarded-For`, que escribe el cliente | `ResolveClientIp()` usa `Connection.RemoteIpAddress` | `SignupAPI/Controllers/SignupsController.cs` |
| 3 | Cuatro servicios perdían sus llaves de Data Protection en cada reinicio: sesiones, antifalsificación, enlaces de correo y PII de SignupAPI | `AddPersistentDataProtectionKeys` con `DataProtection:KeysPath` | SignupAPI, AdminWeb, BizCenterWeb y Signups |
| 4 | El alta tenía `https://localhost:7005` fijo en el navegador | Lee `SignupApiBaseUrl`; `wwwroot/appsettings.Production.json` trae `https://auth-api.mlmconqueror.com` | `Signups.Client` |
| 5 | La licencia de Syncfusion no se registraba | `RegisterLicense(Syncfusion:LicenseKey)` y referencia a `Syncfusion.Licensing` | AdminWeb y BizCenterWeb |
| 6 | El panel de Hangfire de TicketManagementSystem estaba abierto (`Authorization = []`) | Filtro por defecto: solo peticiones locales | `TicketManagementSystem/Program.cs` |
| 7 | Seis `appsettings.json` tenían comentada una cadena de conexión con contraseña a la copia restaurada de la base de producción anterior, y Signups una contraseña de `RootAmbassador` | Líneas borradas | Billing, CommissionEngine, RankEngine, SharedAPICenter, TicketManagementSystem y Signups |

Las pruebas `DetrasDelBalanceadorTests` (BizCenter.Tests) comprueban dos cosas. La primera es que, desde la red de la VPC, se toma la IP que añadió el balanceador y no la que escribió el cliente, y que desde fuera de la VPC la cabecera no cambia nada. La segunda es que lo que protege un pod lo lee otro con la misma carpeta de llaves.

### 2.1 Cómo se comporta cada cambio en producción

- **Forwarded headers:**
  - Con `ForwardedHeaders__KnownNetwork` igual al CIDR de la VPC (Fase 10), solo se confía en el salto que viene del ALB.
  - Sin esa clave (desarrollo) solo se confía en la propia máquina.
  - El límite de peticiones por IP usa la IP resultante, porque en producción `IpRateLimiting__RealIpHeader` va vacío (Fase 10).
- **Llaves de Data Protection:**
  - Con `DataProtection__KeysPath=/keys` se guardan en el volumen EFS de cada servicio.
  - Los nombres de aplicación (`MLMConqueror.SignupAPI`, `MLMConqueror.AdminWeb`, `MLMConqueror.BizCenterWeb`, `MLMConqueror.Signups`) no se cambian nunca: cambiarlos invalida todo lo protegido.
  - No se usa la tabla `DataProtectionKeys` de la base, que es del anillo de credenciales de las pasarelas.
- **Alta en el navegador:** si el dominio cambia, se edita `Signups.Client/wwwroot/appsettings.Production.json` y se reconstruye la imagen de `signups`.
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

### 2.6 Cierre de la Fase 0

En Windows, con la rama `fase0-eks` integrada en `master`:

```
dotnet build MLMConquerorGlobalEdition.slnx -c Release
dotnet test MLMConquerorGlobalEdition.slnx -c Release
git log --oneline -1   # anotar el commit: es el que se despliega
```

Las pruebas tienen que estar en verde. Se anota el commit de `master` que resulte, porque es el que se construye en la Fase 7.

---

## 3. Fase 1 — Cuenta de AWS y herramientas

### 3.1 Cuenta y acceso

1. Entrar con el usuario raíz y activar MFA en él. No se vuelve a usar para el trabajo diario.
2. En IAM Identity Center, crear un usuario administrador con MFA y el permiso `AdministratorAccess`. Todo lo que sigue se hace con ese usuario.
3. En Billing, crear un presupuesto (AWS Budgets) mensual con aviso al 80% y al 100%.
4. En la región `us-east-1`, activar CloudTrail con un trail para toda la cuenta (Management events, en un bucket propio).

### 3.2 Herramientas en WSL (Ubuntu 24.04)

```
sudo apt-get update
sudo apt-get install -y unzip jq gettext-base openssl git curl ca-certificates
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
docker version; dotnet --version; envsubst --version | head -1
```

### 3.3 Iniciar sesión en AWS desde la terminal

```
aws configure sso          # perfil: mlm-prod, region: us-east-1, salida: json
export AWS_PROFILE=mlm-prod
aws sso login
aws sts get-caller-identity
```

### 3.4 Archivo de variables de la guía

Se crea una sola vez. En cada terminal nueva se carga con `source ~/mlm-prod.env`.

```
cat > ~/mlm-prod.env <<'EOF'
export AWS_PROFILE=mlm-prod
export AWS_REGION=us-east-1
export CUENTA=$(aws sts get-caller-identity --query Account --output text)
export CLUSTER=mlmconqueror-prod
export NS=mlmconqueror
export DOMINIO=mlmconqueror.com
export VPC_CIDR=10.20.0.0/16
export ECR=$CUENTA.dkr.ecr.$AWS_REGION.amazonaws.com
export BUCKET=mlmconqueror-prod-archivos-$CUENTA
export SES_FROM=no-reply@$DOMINIO
export TZ_NEGOCIO=America/New_York
export REPO=~/MLMConquerorGlobalMicroServices
EOF
source ~/mlm-prod.env
```

A lo largo de la guía se van agregando más variables con `echo 'export X=...' >> ~/mlm-prod.env`.

Esta es la función que aplica manifiestos. Sustituye solo las variables de la guía y deja intacto cualquier otro `$` del YAML:

```
cat >> ~/mlm-prod.env <<'EOF'
VARS_GUIA='$AWS_REGION $CUENTA $CLUSTER $NS $DOMINIO $VPC_CIDR $ECR $BUCKET $SES_FROM
 $TZ_NEGOCIO $TAG $CERT_ARN $EFS_ID $RDS_HOST $WAF_ARN $JWT_PUBLICA'
aplicar() { envsubst "$VARS_GUIA" < "$1" | kubectl apply -f -; }
EOF
source ~/mlm-prod.env
```

### 3.5 Copia limpia del repositorio

```
git clone https://github.com/NickSagaracorp/MLMConquerorGlobalMicroServices.git $REPO
cd $REPO && git checkout master && git log --oneline -1
mkdir -p ~/mlm-k8s     # aqui se guardan los manifiestos de las fases 9 a 11
```

El commit que muestra tiene que ser el anotado al cerrar la Fase 0.

---

## 4. Fase 2 — DNS y certificado

### 4.1 Zona de Route 53

Si el dominio aún no tiene zona en Route 53:

```
aws route53 create-hosted-zone --name $DOMINIO --caller-reference "mlm-$(date +%s)"
```

Si el dominio está registrado en otro proveedor, se copian los 4 servidores `NS` que devuelve el comando a los servidores de nombre del dominio en ese proveedor.

```
export ZONA_ID=$(aws route53 list-hosted-zones-by-name --dns-name $DOMINIO \
  --query 'HostedZones[0].Id' --output text | sed 's#/hostedzone/##')
echo "export ZONA_ID=$ZONA_ID" >> ~/mlm-prod.env
dig +short NS $DOMINIO      # tiene que devolver los NS de Route 53
```

### 4.2 Certificado de ACM (dominio y comodín)

```
export CERT_ARN=$(aws acm request-certificate --domain-name $DOMINIO \
  --subject-alternative-names "*.$DOMINIO" --validation-method DNS \
  --query CertificateArn --output text)
echo "export CERT_ARN=$CERT_ARN" >> ~/mlm-prod.env
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

### 5.1 Versión de Kubernetes

```
aws eks describe-cluster-versions --default-only \
  --query 'clusterVersions[0].clusterVersion' --output text
```

La versión que devuelve es la que se usa en `K8S_VERSION`. Se elige la versión por defecto de EKS, no la más nueva.

```
echo "export K8S_VERSION=<version>" >> ~/mlm-prod.env && source ~/mlm-prod.env
```

### 5.2 Definición del clúster

```
cat > ~/mlm-k8s/cluster.yaml <<EOF
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
eksctl create cluster -f ~/mlm-k8s/cluster.yaml
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
cat >> ~/mlm-prod.env <<EOF
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

### 6.2 External Secrets Operator

```
helm repo add external-secrets https://charts.external-secrets.io && helm repo update
helm install external-secrets external-secrets/external-secrets \
  -n external-secrets --create-namespace --set installCRDs=true
kubectl get pods -n external-secrets                 # 3 pods Running
kubectl api-resources | grep -i externalsecret       # anotar la version (external-secrets.io/v1)
```

Los manifiestos de esta guía usan `external-secrets.io/v1`. Si el comando anterior muestra otra versión, se usa esa.

La cuenta de servicio con permiso de lectura se crea en la Fase 6, cuando existan la llave KMS y los secretos.

### 6.3 CloudWatch Container Insights (métricas y logs)

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

### 7.1 Grupos de seguridad

Solo los nodos del clúster pueden llegar a SQL Server (1433), a Redis (6379) y a EFS (2049):

```
for n in rds redis efs; do
  aws ec2 create-security-group --group-name mlm-prod-$n --description "mlm prod $n" \
    --vpc-id $VPC_ID --query GroupId --output text > /tmp/sg-$n
done
export SG_RDS=$(cat /tmp/sg-rds) SG_REDIS=$(cat /tmp/sg-redis) SG_EFS=$(cat /tmp/sg-efs)
aws ec2 authorize-security-group-ingress --group-id $SG_RDS --protocol tcp --port 1433 \
  --source-group $CLUSTER_SG
aws ec2 authorize-security-group-ingress --group-id $SG_REDIS --protocol tcp --port 6379 \
  --source-group $CLUSTER_SG
aws ec2 authorize-security-group-ingress --group-id $SG_EFS --protocol tcp --port 2049 \
  --source-group $CLUSTER_SG
cat >> ~/mlm-prod.env <<EOF
export SG_RDS=$SG_RDS
export SG_REDIS=$SG_REDIS
export SG_EFS=$SG_EFS
EOF
```

### 7.2 Llave KMS de la aplicación

Cifra los secretos, la base, Redis, EFS y S3:

```
export KMS_ARN=$(aws kms create-key --description "MLM Conqueror prod" \
  --query KeyMetadata.Arn --output text)
aws kms create-alias --alias-name alias/mlmconqueror-prod --target-key-id $KMS_ARN
aws kms enable-key-rotation --key-id $KMS_ARN
echo "export KMS_ARN=$KMS_ARN" >> ~/mlm-prod.env
```

### 7.3 RDS for SQL Server

**Paso 1.** Elegir la versión 2022 (16.00) más reciente:

```
aws rds describe-db-engine-versions --engine sqlserver-se \
  --query 'DBEngineVersions[?starts_with(EngineVersion,`16.00`)].EngineVersion' --output text
```

Se anota la última que aparece como `RDS_VERSION` en `~/mlm-prod.env`.

**Paso 2.** Grupo de subredes y creación de la instancia. La zona horaria **se fija aquí y no se puede cambiar después**; tiene que ser la misma que `TZ_NEGOCIO`, porque el código usa `GETDATE()`.

```
aws rds create-db-subnet-group --db-subnet-group-name mlm-prod \
  --db-subnet-group-description "mlm prod" --subnet-ids $SUBNETS_PRIVADAS
aws rds create-db-instance --db-instance-identifier mlm-prod-sql \
  --engine sqlserver-se --engine-version $RDS_VERSION --license-model license-included \
  --db-instance-class db.m6i.xlarge --allocated-storage 200 --storage-type gp3 \
  --max-allocated-storage 1000 --multi-az \
  --master-username mlmadmin --manage-master-user-password \
  --master-user-secret-kms-key-id $KMS_ARN \
  --db-subnet-group-name mlm-prod --vpc-security-group-ids $SG_RDS \
  --no-publicly-accessible --storage-encrypted --kms-key-id $KMS_ARN \
  --backup-retention-period 14 --preferred-backup-window 07:00-08:00 \
  --preferred-maintenance-window sun:08:30-sun:09:30 \
  --timezone "Eastern Standard Time" \
  --enable-performance-insights --performance-insights-kms-key-id $KMS_ARN \
  --deletion-protection --copy-tags-to-snapshot
aws rds wait db-instance-available --db-instance-identifier mlm-prod-sql   # 30-60 min
```

La contraseña del usuario maestro la genera RDS y la guarda en Secrets Manager (`--manage-master-user-password`). Nadie la escribe.

**Paso 3.** Anotar el host:

```
export RDS_HOST=$(aws rds describe-db-instances --db-instance-identifier mlm-prod-sql \
  --query 'DBInstances[0].Endpoint.Address' --output text)
echo "export RDS_HOST=$RDS_HOST" >> ~/mlm-prod.env
```

La base de datos y el usuario de la aplicación se crean en la Fase 8, desde dentro del clúster: la instancia no es accesible desde Internet.

### 7.4 ElastiCache (Redis) con TLS y token

```
openssl rand -base64 48 | tr -d '/+=' | cut -c1-64 > /tmp/redis-token
aws elasticache create-cache-subnet-group --cache-subnet-group-name mlm-prod \
  --cache-subnet-group-description "mlm prod" --subnet-ids $SUBNETS_PRIVADAS
aws elasticache create-replication-group --replication-group-id mlm-prod-redis \
  --replication-group-description "mlm prod" --engine redis \
  --cache-node-type cache.t4g.medium --num-node-groups 1 --replicas-per-node-group 1 \
  --automatic-failover-enabled --multi-az-enabled \
  --cache-subnet-group-name mlm-prod --security-group-ids $SG_REDIS \
  --transit-encryption-enabled --at-rest-encryption-enabled --kms-key-id $KMS_ARN \
  --auth-token "$(cat /tmp/redis-token)" --snapshot-retention-limit 3
aws elasticache wait replication-group-available --replication-group-id mlm-prod-redis
export REDIS_HOST=$(aws elasticache describe-replication-groups \
  --replication-group-id mlm-prod-redis \
  --query 'ReplicationGroups[0].NodeGroups[0].PrimaryEndpoint.Address' --output text)
echo "export REDIS_HOST=$REDIS_HOST" >> ~/mlm-prod.env
```

`/tmp/redis-token` se usa en la Fase 6 y se borra allí.

### 7.5 EFS (disco compartido) y clase de almacenamiento

```
export EFS_ID=$(aws efs create-file-system --encrypted --kms-key-id $KMS_ARN \
  --performance-mode generalPurpose --throughput-mode elastic \
  --tags Key=Name,Value=mlm-prod --query FileSystemId --output text)
echo "export EFS_ID=$EFS_ID" >> ~/mlm-prod.env
sleep 20
for s in $SUBNETS_PRIVADAS; do
  aws efs create-mount-target --file-system-id $EFS_ID --subnet-id $s --security-groups $SG_EFS
done
aws efs put-backup-policy --file-system-id $EFS_ID --backup-policy Status=ENABLED
```

Clase de almacenamiento. Los archivos quedan con el usuario 1654, que es el `APP_UID` de las imágenes de .NET:

```
cat > ~/mlm-k8s/storageclass.yaml <<'EOF'
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
aplicar ~/mlm-k8s/storageclass.yaml
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

## 8. Fase 6 — Secretos en AWS Secrets Manager

### 8.1 Nombres de los secretos

Todos los secretos están bajo `mlmconqueror/prod/`. Cada uno es un JSON cuyas claves son **el nombre exacto de la variable de entorno** que lee .NET (`Seccion__Clave`). Así, External Secrets los pasa al pod sin traducir nada.

| Secreto | Claves | Lo leen |
|---|---|---|
| `mlmconqueror/prod/datos` | `ConnectionStrings__DefaultConnection`, `ConnectionStrings__HangFire`, `ConnectionStrings__Redis` | Los 8 servicios con base |
| `mlmconqueror/prod/jwt-firma` | `Jwt__PrivateKeyBase64` | SignupAPI y AdminAPI, y nadie más |
| `mlmconqueror/prod/signupapi` | `RootAmbassador__Email`, `RootAmbassador__Password`, `Entrax__KeyId`, `Entrax__Secret` | SignupAPI |
| `mlmconqueror/prod/dp-gateway` | `pfxBase64`, `DataProtection__Certificate__Password` | AdminAPI, Billing y BizCenter |
| `mlmconqueror/prod/sharedapicenter` | `ExternalApi__ApiKey` | SharedAPICenter |
| `mlmconqueror/prod/portales` | `Syncfusion__LicenseKey` | AdminWeb y BizCenterWeb |
| `mlmconqueror/prod/firebase` | `firebaseJson` | Billing, BizCenter, SignupAPI, CommissionEngine, RankEngine, SharedAPICenter y TicketManagementSystem |
| `mlmconqueror/prod/twilio` | `Notifications__Sms__Twilio__AccountSid`, `Notifications__Sms__Twilio__AuthToken` | Se crea en la Fase 14 |

### 8.2 Carpeta de trabajo temporal

Todo lo que se genera en esta fase va a una carpeta que solo lee el usuario y que se borra al final.

```
umask 077
mkdir -p ~/mlm-secretos && cd ~/mlm-secretos
set +o history      # no guardar en el historial lo que se escriba en esta fase
```

### 8.3 Llaves JWT

Formato **PKCS#8 DER en base64**, de 2048 bits. Es el mismo procedimiento de `docs/deployment/rotacion-llaves-jwt.md`. No se usa PEM ni `genrsa`.

```
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER -out jwt-privada.der
openssl rsa -inform DER -in jwt-privada.der -pubout -outform DER -out jwt-publica.der
base64 -w0 jwt-privada.der > jwt-privada.b64
base64 -w0 jwt-publica.der > jwt-publica.b64
```

La pública no es secreta: va a la configuración de todos los servicios (Fase 10).

```
echo "export JWT_PUBLICA=$(cat jwt-publica.b64)" >> ~/mlm-prod.env
```

### 8.4 Certificado del anillo de credenciales de las pasarelas

AdminAPI cifra con él las credenciales de PayQuicker, i-Payout y Volet. Billing y BizCenter las descifran. Los tres tienen que tener **el mismo PFX**: si se pierde, todas las credenciales guardadas quedan ilegibles.

```
openssl rand -base64 32 | tr -d '/+=' > pfx-pass
openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
  -keyout dp.key -out dp.crt -subj "/CN=MLMConqueror Gateway Credentials" \
  -addext "keyUsage=critical,keyEncipherment,dataEncipherment"
openssl pkcs12 -export -inkey dp.key -in dp.crt -out gateway.pfx \
  -passout file:pfx-pass -certpbe AES-256-CBC -keypbe AES-256-CBC -macalg sha256
base64 -w0 gateway.pfx > gateway.pfx.b64
```

**Antes de seguir:** se guarda una copia de `gateway.pfx` y `pfx-pass` fuera de AWS, en el gestor de contraseñas de la empresa. Es la única forma de recuperar las credenciales si alguien borra el secreto.

### 8.5 Base de datos: contraseña del usuario de la aplicación

```
openssl rand -base64 36 | tr -d '/+=;' | cut -c1-40 > sql-app-pass
```

### 8.6 Crear los secretos

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
aws secretsmanager create-secret --name mlmconqueror/prod/datos --kms-key-id $KMS_ARN \
  --secret-string file://datos.json
```

**Llave de firma JWT:**

```
jq -n --arg k "$(cat jwt-privada.b64)" '{"Jwt__PrivateKeyBase64": $k}' > jwt.json
aws secretsmanager create-secret --name mlmconqueror/prod/jwt-firma --kms-key-id $KMS_ARN \
  --secret-string file://jwt.json
```

**SignupAPI.** La cuenta raíz que el arranque siembra. El correo es un buzón real de la empresa, nunca uno público. Los datos de Entrax quedan vacíos hasta la Fase 14:

```
read -p "Correo de la cuenta raiz: " ROOT_MAIL
openssl rand -base64 24 | tr -d '/+=' > root-pass
jq -n --arg e "$ROOT_MAIL" --arg p "$(cat root-pass)Aa1#" '{
  "RootAmbassador__Email": $e, "RootAmbassador__Password": $p,
  "Entrax__KeyId": "", "Entrax__Secret": "" }' > signupapi.json
aws secretsmanager create-secret --name mlmconqueror/prod/signupapi --kms-key-id $KMS_ARN \
  --secret-string file://signupapi.json
```

La contraseña de la cuenta raíz (el contenido de `root-pass` seguido de `Aa1#`) se guarda en el gestor de contraseñas y se cambia en el primer inicio de sesión.

**Anillo de las pasarelas:**

```
jq -n --arg b "$(cat gateway.pfx.b64)" --arg p "$(cat pfx-pass)" '{
  "pfxBase64": $b, "DataProtection__Certificate__Password": $p }' > dp.json
aws secretsmanager create-secret --name mlmconqueror/prod/dp-gateway --kms-key-id $KMS_ARN \
  --secret-string file://dp.json
```

**API externa (`X-Api-Key`):**

```
jq -n --arg k "$(openssl rand -hex 32)" '{"ExternalApi__ApiKey": $k}' > ext.json
aws secretsmanager create-secret --name mlmconqueror/prod/sharedapicenter \
  --kms-key-id $KMS_ARN --secret-string file://ext.json
```

**Portales (Syncfusion):**

```
read -s -p "Clave de licencia de Syncfusion: " SF; echo
jq -n --arg k "$SF" '{"Syncfusion__LicenseKey": $k}' > portales.json
aws secretsmanager create-secret --name mlmconqueror/prod/portales --kms-key-id $KMS_ARN \
  --secret-string file://portales.json
```

**Firebase.** Se copia antes a `~/mlm-secretos/firebase.json` el JSON de la cuenta de servicio, descargado de la consola de Firebase (Configuración del proyecto, Cuentas de servicio):

```
jq -n --arg j "$(cat firebase.json)" '{"firebaseJson": $j}' > fb.json
aws secretsmanager create-secret --name mlmconqueror/prod/firebase --kms-key-id $KMS_ARN \
  --secret-string file://fb.json
```

Si no hay proyecto de Firebase, este secreto no se crea, y en las Fases 9, 10 y 11 se omiten el montaje `firebase` y la variable `Firebase__CredentialsFilePath`.

### 8.7 Borrar la carpeta de trabajo

```
cd ~ && shred -u ~/mlm-secretos/* /tmp/redis-token 2>/dev/null; rm -rf ~/mlm-secretos
unset SF SQL REDIS ROOT_MAIL
set -o history
aws secretsmanager list-secrets --filters Key=name,Values=mlmconqueror/prod \
  --query 'SecretList[].Name'
```

**Comprobar:** la lista muestra los 7 secretos de la tabla (6 si no hay Firebase). La contraseña de `mlm_app` hace falta en la Fase 8, y se lee del secreto `datos` en ese momento.

### 8.8 Permiso de External Secrets para leer los secretos

```
cat > /tmp/eso-policy.json <<EOF
{"Version":"2012-10-17","Statement":[
 {"Effect":"Allow","Action":["secretsmanager:GetSecretValue","secretsmanager:DescribeSecret"],
  "Resource":"arn:aws:secretsmanager:$AWS_REGION:$CUENTA:secret:mlmconqueror/prod/*"},
 {"Effect":"Allow","Action":"kms:Decrypt","Resource":"$KMS_ARN"}]}
EOF
aws iam create-policy --policy-name MLM-ExternalSecretsRead \
  --policy-document file:///tmp/eso-policy.json
eksctl create iamserviceaccount --cluster $CLUSTER --namespace external-secrets \
  --name eso-aws --role-name MLM-ExternalSecretsRead \
  --attach-policy-arn arn:aws:iam::$CUENTA:policy/MLM-ExternalSecretsRead --approve
```

Almacén de secretos para todo el clúster:

```
cat > ~/mlm-k8s/secretstore.yaml <<'EOF'
apiVersion: external-secrets.io/v1
kind: ClusterSecretStore
metadata:
  name: aws-secrets-manager
spec:
  provider:
    aws:
      service: SecretsManager
      region: ${AWS_REGION}
      auth:
        jwt:
          serviceAccountRef:
            name: eso-aws
            namespace: external-secrets
EOF
aplicar ~/mlm-k8s/secretstore.yaml
kubectl get clustersecretstore aws-secrets-manager     # STATUS Valid, READY True
```

---

## 9. Fase 7 — ECR e imágenes

### 9.1 Repositorios

```
SERVICIOS="signupapi adminapi billing bizcenter commissionengine rankengine \
ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups"
echo "export SERVICIOS=\"$SERVICIOS\"" >> ~/mlm-prod.env
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
declare -f proyecto >> ~/mlm-prod.env
```

### 9.3 Construir y subir las 11 imágenes

La etiqueta es el commit corto. Así cada imagen dice de qué código sale.

```
cd $REPO && git pull && git status --short     # sin cambios locales
export TAG=$(git rev-parse --short=7 HEAD)
echo "export TAG=$TAG" >> ~/mlm-prod.env
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
MASTER_SECRET=$(aws rds describe-db-instances --db-instance-identifier mlm-prod-sql \
  --query 'DBInstances[0].MasterUserSecret.SecretArn' --output text)
MASTER_PASS=$(aws secretsmanager get-secret-value --secret-id $MASTER_SECRET \
  --query SecretString --output text | jq -r .password)
APP_PASS=$(aws secretsmanager get-secret-value --secret-id mlmconqueror/prod/datos \
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
aws rds create-db-snapshot --db-instance-identifier mlm-prod-sql \
  --db-snapshot-identifier mlm-prod-sql-esquema-$TAG
kubectl delete pod -n $NS sqlcmd
unset MASTER_PASS APP_PASS
```

---

## 11. Fase 9 — Permisos de AWS para los pods, volúmenes y secretos en el clúster

### 11.1 Cuenta de servicio con S3 y SES (IRSA)

La usan signupapi, adminapi, billing, bizcenter y rankengine. Los otros seis usan `mlm-sin-aws`, que no tiene ningún permiso de AWS.

```
cat > /tmp/apps-policy.json <<EOF
{"Version":"2012-10-17","Statement":[
 {"Effect":"Allow","Action":["s3:GetObject","s3:PutObject","s3:DeleteObject"],
  "Resource":"arn:aws:s3:::$BUCKET/*"},
 {"Effect":"Allow","Action":"s3:ListBucket","Resource":"arn:aws:s3:::$BUCKET"},
 {"Effect":"Allow","Action":["kms:Decrypt","kms:GenerateDataKey"],"Resource":"$KMS_ARN"},
 {"Effect":"Allow","Action":["ses:SendEmail","ses:SendRawEmail"],"Resource":"*",
  "Condition":{"StringEquals":{"ses:FromAddress":"$SES_FROM"}}}]}
EOF
aws iam create-policy --policy-name MLM-Apps --policy-document file:///tmp/apps-policy.json
eksctl create iamserviceaccount --cluster $CLUSTER --namespace $NS --name mlm-aws \
  --role-name MLM-Apps --attach-policy-arn arn:aws:iam::$CUENTA:policy/MLM-Apps --approve
kubectl create serviceaccount mlm-sin-aws -n $NS
```

En ningún servicio se configuran `AWS__AccessKey`, `AWS__SecretKey` ni `AWS__Credentials__*`. Vacíos, el SDK usa la identidad del pod.

### 11.2 Volúmenes en EFS

Se crea `~/mlm-k8s/volumenes.yaml` con 7 PVC: `uploads` (adjuntos de AdminAPI y BizCenter), `recibos` (Billing y AdminAPI), `certificados` (RankEngine) y una de llaves por cada servicio que protege cookies:

```
cat > ~/mlm-k8s/volumenes.yaml <<'EOF'
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
aplicar ~/mlm-k8s/volumenes.yaml
kubectl get pvc -n $NS      # las 7 en Bound
```

En EFS el tamaño pedido es solo un requisito de la API, no un límite: se paga lo que se usa.

### 11.3 Secretos de Kubernetes desde Secrets Manager

Por cada servicio se crea un `ExternalSecret` que junta los secretos que necesita en un solo Secret, más dos que se montan como archivo. Los secretos se refrescan cada hora. Un pod lee las variables al arrancar, así que después de cambiar un secreto hay que reiniciar los servicios que lo usan (sección 15.4).

```
cat > ~/mlm-k8s/externalsecrets.yaml <<'EOF'
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: signupapi, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: signupapi-secret}
  dataFrom:
    - extract: {key: mlmconqueror/prod/datos}
    - extract: {key: mlmconqueror/prod/jwt-firma}
    - extract: {key: mlmconqueror/prod/signupapi}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: adminapi, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: adminapi-secret}
  dataFrom:
    - extract: {key: mlmconqueror/prod/datos}
    - extract: {key: mlmconqueror/prod/jwt-firma}
  data:
    - secretKey: DataProtection__Certificate__Password
      remoteRef: {key: mlmconqueror/prod/dp-gateway,
                  property: DataProtection__Certificate__Password}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: billing, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: billing-secret}
  dataFrom:
    - extract: {key: mlmconqueror/prod/datos}
  data:
    - secretKey: DataProtection__Certificate__Password
      remoteRef: {key: mlmconqueror/prod/dp-gateway,
                  property: DataProtection__Certificate__Password}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: bizcenter, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: bizcenter-secret}
  dataFrom:
    - extract: {key: mlmconqueror/prod/datos}
  data:
    - secretKey: DataProtection__Certificate__Password
      remoteRef: {key: mlmconqueror/prod/dp-gateway,
                  property: DataProtection__Certificate__Password}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: datos, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: datos-secret}
  dataFrom:
    - extract: {key: mlmconqueror/prod/datos}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: sharedapicenter, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: sharedapicenter-secret}
  dataFrom:
    - extract: {key: mlmconqueror/prod/datos}
    - extract: {key: mlmconqueror/prod/sharedapicenter}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: portales, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: portales-secret}
  dataFrom:
    - extract: {key: mlmconqueror/prod/portales}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: gateway-pfx, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: gateway-pfx}
  data:
    - secretKey: gateway.pfx
      remoteRef: {key: mlmconqueror/prod/dp-gateway, property: pfxBase64,
                  decodingStrategy: Base64}
---
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata: {name: firebase, namespace: ${NS}}
spec:
  refreshInterval: 1h
  secretStoreRef: {kind: ClusterSecretStore, name: aws-secrets-manager}
  target: {name: firebase}
  data:
    - secretKey: firebase.json
      remoteRef: {key: mlmconqueror/prod/firebase, property: firebaseJson}
EOF
aplicar ~/mlm-k8s/externalsecrets.yaml
kubectl get externalsecret -n $NS        # todos SecretSynced / Ready True
```

`datos-secret` lo usan commissionengine, rankengine y ticketmanagementsystem, que solo necesitan la base y Redis.

---

## 12. Fase 10 — Configuración de los servicios (ConfigMaps)

Solo se sobrescribe lo que en producción tiene que ser distinto de `appsettings.json`. Lo demás (Entrax `Countries`, `IpRateLimiting:GeneralRules`, límites del 2FA, `MyLinks`, etc.) queda como viene en el repositorio.

Hay tres reglas que no se cambian:
- `AllowedHosts=*`: el ALB y Kubernetes comprueban la salud del pod con su IP como `Host`. Con `localhost` (el valor del repositorio) responderían 400 y los pods nunca quedarían sanos. El filtro de nombres lo hace el ALB.
- `Signup__CardEnabled`, `Membership__UpgradeEnabled`, `Billing__TokenPurchase__CardEnabled` y `Billing__Recurring__CardEnabled` van en `false`. Son bloqueantes de producción (`docs/deployment/pruebas-de-seguridad.md`).
- `Auth__TwoFactor__MandatoryRoles` no se define en el primer despliegue. Se enciende en la Fase 14, siguiendo `docs/deployment/verificacion-2fa.md`.

```
cat > ~/mlm-k8s/configmaps.yaml <<'EOF'
apiVersion: v1
kind: ConfigMap
metadata: {name: comun, namespace: ${NS}}
data:
  ASPNETCORE_ENVIRONMENT: Production
  TZ: ${TZ_NEGOCIO}
  AllowedHosts: "*"
  ForwardedHeaders__KnownNetwork: ${VPC_CIDR}
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
metadata: {name: firebase-ruta, namespace: ${NS}}
data:
  Firebase__CredentialsFilePath: /secrets/firebase/firebase.json
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
  Services__SignupAPI: http://signupapi.${NS}.svc.cluster.local/
  Services__BillingBaseUrl: http://billing.${NS}.svc.cluster.local/
  Services__Billing: http://billing.${NS}.svc.cluster.local
  Services__BizCenter: http://bizcenter.${NS}.svc.cluster.local
  Services__CommissionEngine: http://commissionengine.${NS}.svc.cluster.local
  Services__RankEngine: http://rankengine.${NS}.svc.cluster.local
  Services__TicketManagementSystem: http://ticketmanagementsystem.${NS}.svc.cluster.local
  Services__SharedAPICenter: http://sharedapicenter.${NS}.svc.cluster.local
  DataProtection__Certificate__Path: /secrets/dp/gateway.pfx
  ReceiptStorage__LocalPath: /data/payout-receipts
  ReceiptStorage__PublicBaseUrl: https://admin-api.${DOMINIO}
  Billing__TokenPurchase__CardEnabled: "false"
  Billing__Recurring__CardEnabled: "false"
---
apiVersion: v1
kind: ConfigMap
metadata: {name: billing, namespace: ${NS}}
data:
  DataProtection__Certificate__Path: /secrets/dp/gateway.pfx
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
  Services__RankEngineBaseUrl: http://rankengine.${NS}.svc.cluster.local/
  Services__BillingBaseUrl: http://billing.${NS}.svc.cluster.local/
  AWS__S3__BucketName: ${BUCKET}
  AWS__S3__Region: ${AWS_REGION}
  DataProtection__Certificate__Path: /secrets/dp/gateway.pfx
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
  PortalSessionBounce__PublicBaseUrl: https://signup.${DOMINIO}
  PortalSessionBounce__Portals__0__Name: BizCenterWeb
  PortalSessionBounce__Portals__0__SignOutUrl: https://app.${DOMINIO}/account/logout
  PortalSessionBounce__Portals__0__ProbeUrl: http://bizcenterweb.${NS}.svc.cluster.local/health
  PortalSessionBounce__Portals__1__Name: AdminWeb
  PortalSessionBounce__Portals__1__SignOutUrl: https://admin.${DOMINIO}/account/logout
  PortalSessionBounce__Portals__1__ProbeUrl: http://adminweb.${NS}.svc.cluster.local/health
  DataProtection__KeysPath: /keys
EOF
aplicar ~/mlm-k8s/configmaps.yaml
kubectl get configmap -n $NS
```

Hay tres cosas que dependen de que el portal de alta y los portales compartan dominio:
- El alta y los portales comparten el dominio registrable (`*.${DOMINIO}`). Eso permite que la cookie `SameSite=Strict` de los portales viaje en el rebote de cierre de sesión del alta.
- Si algún día el alta se sirve desde otro dominio, se pone `PortalSessionBounce__Enabled` en `false`.
- `ReceiptStorage__PublicBaseUrl` solo forma la URL que se guarda con cada recibo. El recibo se descarga a través de AdminAPI, que lo lee del volumen `recibos`.

---

## 13. Fase 11 — Despliegue de los servicios

### 13.1 Manifiestos de los servicios

Todos los Deployments tienen las mismas características:
- Arrancan con 1 réplica, que se sube en la Fase 13 (sección 15.1).
- Corren como usuario no root, con el sistema de archivos raíz de solo lectura salvo `/tmp`.
- La prueba de arranque espera hasta 10 minutos, porque el primer arranque aplica las semillas.
- La prueba de disponibilidad usa `/health`.
- La de vida comprueba el puerto, para que una caída de la base no reinicie todos los pods.

Se crea un archivo completo por servicio en `~/mlm-k8s/svc/`. Primero se define la función que escribe la parte común:

```
mkdir -p ~/mlm-k8s/svc
gen() {  # gen <servicio> <cuenta-servicio> <configmaps> <secrets> <montajes> <volumenes>
cat > ~/mlm-k8s/svc/$1.yaml <<EOF
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
      serviceAccountName: $2
      automountServiceAccountToken: $( [ "$2" = mlm-aws ] && echo true || echo false )
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
$3
$4
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
$5
      volumes:
        - {name: tmp, emptyDir: {}}
$6
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
cm()  { for c in "$@"; do echo "            - configMapRef: {name: $c}"; done; }
sec() { for s in "$@"; do echo "            - secretRef: {name: $s}"; done; }
```

Fragmentos de montaje que se repiten:

```
M_FB='            - {name: firebase, mountPath: /secrets/firebase, readOnly: true}'
V_FB='        - {name: firebase, secret: {secretName: firebase}}'
M_DP='            - {name: gateway-pfx, mountPath: /secrets/dp, readOnly: true}'
V_DP='        - {name: gateway-pfx, secret: {secretName: gateway-pfx}}'
M_KEYS='            - {name: llaves, mountPath: /keys}'
M_UP='            - {name: uploads, mountPath: /app/wwwroot/uploads}'
V_UP='        - {name: uploads, persistentVolumeClaim: {claimName: uploads}}'
M_REC='            - {name: recibos, mountPath: /data/payout-receipts}'
V_REC='        - {name: recibos, persistentVolumeClaim: {claimName: recibos}}'
nl=$'\n'
```

Generar los 11 archivos:

```
gen signupapi mlm-aws "$(cm comun firebase-ruta signupapi)" "$(sec signupapi-secret)" \
  "$M_FB$nl$M_KEYS" \
  "$V_FB$nl        - {name: llaves, persistentVolumeClaim: {claimName: llaves-signupapi}}"
gen adminapi mlm-aws "$(cm comun adminapi)" "$(sec adminapi-secret)" \
  "$M_DP$nl$M_UP$nl$M_REC" "$V_DP$nl$V_UP$nl$V_REC"
gen billing mlm-aws "$(cm comun firebase-ruta billing)" "$(sec billing-secret)" \
  "$M_FB$nl$M_DP$nl$M_REC" "$V_FB$nl$V_DP$nl$V_REC"
gen bizcenter mlm-aws "$(cm comun firebase-ruta bizcenter)" "$(sec bizcenter-secret)" \
  "$M_FB$nl$M_DP$nl$M_UP" "$V_FB$nl$V_DP$nl$V_UP"
gen commissionengine mlm-sin-aws "$(cm comun firebase-ruta)" "$(sec datos-secret)" \
  "$M_FB" "$V_FB"
gen rankengine mlm-aws "$(cm comun firebase-ruta rankengine)" "$(sec datos-secret)" \
  "$M_FB$nl            - {name: certificados, mountPath: /app/wwwroot/certificates}" \
  "$V_FB$nl        - {name: certificados, persistentVolumeClaim: {claimName: certificados}}"
gen ticketmanagementsystem mlm-sin-aws "$(cm comun firebase-ruta)" "$(sec datos-secret)" \
  "$M_FB" "$V_FB"
gen sharedapicenter mlm-sin-aws "$(cm comun firebase-ruta)" "$(sec sharedapicenter-secret)" \
  "$M_FB" "$V_FB"
gen adminweb mlm-sin-aws "$(cm comun adminweb)" "$(sec portales-secret)" "$M_KEYS" \
  "        - {name: llaves, persistentVolumeClaim: {claimName: llaves-adminweb}}"
gen bizcenterweb mlm-sin-aws "$(cm comun bizcenterweb)" "$(sec portales-secret)" "$M_KEYS" \
  "        - {name: llaves, persistentVolumeClaim: {claimName: llaves-bizcenterweb}}"
gen signups mlm-sin-aws "$(cm comun signups)" "" "$M_KEYS" \
  "        - {name: llaves, persistentVolumeClaim: {claimName: llaves-signups}}"
ls ~/mlm-k8s/svc       # 11 archivos
```

Sin Firebase, en todas las llamadas se quitan `firebase-ruta`, `$M_FB` y `$V_FB`.

**Comprobar antes de aplicar.** Validación en el servidor, sin crear nada:

```
for f in ~/mlm-k8s/svc/*.yaml; do
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
  echo "== $s"; aplicar ~/mlm-k8s/svc/$s.yaml
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
| Falta una variable obligatoria | `is not configured` |
| No se puede leer el PFX | `DataProtection` |
| Falla la conexión a la base | `A network-related` (grupo de seguridad) o `certificate chain` (falta la CA de RDS en la imagen) |
| Falla Redis | `Cache:Mode=Required` sin Redis alcanzable |
| Escritura fuera de los volúmenes | `Read-only file system`: se monta un `emptyDir` en la ruta que aparece en el error |

**Comprobar:** `kubectl get pods -n $NS` muestra los 11 en `Running` y `1/1`.

### 13.3 Comprobación interna de salud

```
kubectl run curl -n $NS --rm -i --restart=Never --image=curlimages/curl -- sh -c '
for s in signupapi adminapi billing bizcenter rankengine commissionengine \
  ticketmanagementsystem sharedapicenter adminweb bizcenterweb signups; do
  printf "%-24s" $s; curl -s -o /dev/null -w "%{http_code}\n" http://$s/health; done'
```

Las 11 líneas tienen que mostrar 200.

---

## 14. Fase 12 — Entrada pública: ALB, WAF y DNS

### 14.1 AWS WAF

Reglas administradas de AWS y un límite de 2.000 peticiones por IP cada 5 minutos:

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
export WAF_ARN=$(aws wafv2 create-web-acl --name mlm-prod --scope REGIONAL \
  --default-action Allow={} --rules file:///tmp/waf-rules.json \
  --visibility-config \
  SampledRequestsEnabled=true,CloudWatchMetricsEnabled=true,MetricName=mlm-prod \
  --query Summary.ARN --output text)
echo "export WAF_ARN=$WAF_ARN" >> ~/mlm-prod.env
```

### 14.2 Ingress

Un solo ALB (`group.name: mlmconqueror`) con tres Ingress:
- **Portales:** con afinidad de sesión, que Blazor Server necesita.
- **APIs públicas:** sin afinidad, y con `/swagger` y `/hangfire` bloqueados desde fuera.
- **Rutas parciales:** RankEngine e integraciones, que solo publican lo necesario.

Las rutas de webhooks de SharedAPICenter (`/api/v1/webhooks/*`) no se publican. El propio código las marca como bloqueante: no comprueban firma.

```
cat > ~/mlm-k8s/ingress.yaml <<'EOF'
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: portales
  namespace: ${NS}
  annotations:
    alb.ingress.kubernetes.io/group.name: mlmconqueror
    alb.ingress.kubernetes.io/group.order: "10"
    alb.ingress.kubernetes.io/scheme: internet-facing
    alb.ingress.kubernetes.io/target-type: ip
    alb.ingress.kubernetes.io/listen-ports: '[{"HTTP":80},{"HTTPS":443}]'
    alb.ingress.kubernetes.io/ssl-redirect: "443"
    alb.ingress.kubernetes.io/certificate-arn: ${CERT_ARN}
    alb.ingress.kubernetes.io/ssl-policy: ELBSecurityPolicy-TLS13-1-2-2021-06
    alb.ingress.kubernetes.io/wafv2-acl-arn: ${WAF_ARN}
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
    alb.ingress.kubernetes.io/scheme: internet-facing
    alb.ingress.kubernetes.io/target-type: ip
    alb.ingress.kubernetes.io/listen-ports: '[{"HTTP":80},{"HTTPS":443}]'
    alb.ingress.kubernetes.io/ssl-redirect: "443"
    alb.ingress.kubernetes.io/certificate-arn: ${CERT_ARN}
    alb.ingress.kubernetes.io/ssl-policy: ELBSecurityPolicy-TLS13-1-2-2021-06
    alb.ingress.kubernetes.io/wafv2-acl-arn: ${WAF_ARN}
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
aplicar ~/mlm-k8s/ingress.yaml
kubectl get ingress -n $NS -w      # esperar a que ADDRESS muestre el nombre del ALB (2-4 min)
```

En RankEngine e integraciones, el ALB solo comprueba la salud del destino en `/health`, que existe en los dos.

### 14.3 Registros DNS

```
ALB_DNS=$(kubectl get ingress portales -n $NS -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
ALB_ZONE=$(aws elbv2 describe-load-balancers \
  --query "LoadBalancers[?DNSName=='$ALB_DNS'].CanonicalHostedZoneId" --output text)
CAMBIOS=""
for h in admin app signup auth-api admin-api api rank integrations; do
  CAMBIOS="$CAMBIOS{\"Action\":\"UPSERT\",\"ResourceRecordSet\":{\"Name\":\"$h.$DOMINIO\",
  \"Type\":\"A\",\"AliasTarget\":{\"HostedZoneId\":\"$ALB_ZONE\",\"DNSName\":\"$ALB_DNS\",
  \"EvaluateTargetHealth\":true}}},"
done
echo "{\"Changes\":[${CAMBIOS%,}]}" > /tmp/dns.json
aws route53 change-resource-record-sets --hosted-zone-id $ZONA_ID --change-batch file:///tmp/dns.json
```

**Comprobar** (a los pocos minutos):

```
for h in admin app signup auth-api admin-api api; do
  printf "%-12s" $h; curl -s -o /dev/null -w "%{http_code}\n" https://$h.$DOMINIO/health; done
curl -s https://auth-api.$DOMINIO/.well-known/jwks.json | jq '.keys[0].kid'
curl -s -o /dev/null -w "%{http_code}\n" https://auth-api.$DOMINIO/swagger     # 404
curl -s -o /dev/null -w "%{http_code}\n" http://admin.$DOMINIO/                # 301
```

Los seis primeros tienen que dar 200, el JWKS tiene que devolver un `kid`, Swagger 404 y HTTP 301 a HTTPS.

### 14.4 Prueba funcional en el navegador

1. `https://signup.${DOMINIO}`: carga el alta, y en las herramientas de desarrollo las llamadas van a `auth-api` (Fase 0, cambio 4).
2. `https://admin.${DOMINIO}`: iniciar sesión con la cuenta raíz y cambiar su contraseña.
3. En Admin, S3 Config: poner el bucket `${BUCKET}` y la región `${AWS_REGION}`, **sin llaves de acceso** (usa la identidad del pod).
4. En Admin, pasarelas de pago: cargar las credenciales de PayQuicker, i-Payout y Volet de producción. Quedan cifradas con el anillo del PFX.
5. Pedir la recuperación de contraseña de un usuario de prueba con buzón real: el correo llega desde `${SES_FROM}`.
6. Con la sesión abierta en `admin`, ejecutar `kubectl rollout restart deployment/adminweb -n $NS`. Al terminar, la sesión sigue abierta (Fase 0, cambio 3).
7. Subir un adjunto a un ticket desde el portal de miembros y abrirlo desde administración (volumen `uploads`).
8. Revisar que los portales no muestran el aviso de licencia de Syncfusion.

---

## 15. Fase 13 — Operación: réplicas, paneles, alarmas y copias

### 15.1 Subir a 2 réplicas

Se hace después de la prueba funcional:

```
for s in $SERVICIOS; do kubectl scale deployment/$s -n $NS --replicas=2; done
kubectl get pods -n $NS -o wide     # 22 pods, repartidos por zona
```

Para que el número de réplicas no vuelva a 1 en el siguiente despliegue, se cambia `replicas: 1` por `replicas: 2` en los 11 archivos de `~/mlm-k8s/svc/`.

### 15.2 Paneles de Hangfire

Ningún panel se publica. Se abren con un túnel local:

```
kubectl port-forward -n $NS deployment/billing 9001:8080
# navegador: http://localhost:9001/hangfire
```

En cada panel se revisa que en Servers haya un servidor por réplica, que escucha su cola (`billing*`, `bizcenter`, `signups`, `commissions`, `rank`, `tickets`), y que los trabajos recurrentes tengan próxima ejecución. Las horas de los trabajos recurrentes están en UTC.

### 15.3 Alarmas

```
export SNS_ARN=$(aws sns create-topic --name mlm-prod-alarmas --query TopicArn --output text)
read -p "Correo de guardia: " GUARDIA
aws sns subscribe --topic-arn $SNS_ARN --protocol email --notification-endpoint "$GUARDIA"
alarma() { aws cloudwatch put-metric-alarm --alarm-name "$1" --namespace "$2" \
  --metric-name "$3" --dimensions "$4" --statistic "$5" --period 300 \
  --evaluation-periods 2 --threshold "$6" --comparison-operator "$7" \
  --alarm-actions $SNS_ARN --ok-actions $SNS_ARN; }
alarma rds-cpu AWS/RDS CPUUtilization Name=DBInstanceIdentifier,Value=mlm-prod-sql \
  Average 80 GreaterThanThreshold
alarma rds-disco AWS/RDS FreeStorageSpace Name=DBInstanceIdentifier,Value=mlm-prod-sql \
  Minimum 21474836480 LessThanThreshold
alarma redis-memoria AWS/ElastiCache DatabaseMemoryUsagePercentage \
  Name=ReplicationGroupId,Value=mlm-prod-redis Average 80 GreaterThanThreshold
ALB_ID=$(aws elbv2 describe-load-balancers \
  --query "LoadBalancers[?DNSName=='$ALB_DNS'].LoadBalancerArn" \
  --output text | sed 's#.*:loadbalancer/##')
alarma alb-5xx AWS/ApplicationELB HTTPCode_Target_5XX_Count Name=LoadBalancer,Value=$ALB_ID \
  Sum 50 GreaterThanThreshold
alarma alb-destinos-caidos AWS/ApplicationELB UnHealthyHostCount Name=LoadBalancer,Value=$ALB_ID \
  Maximum 0 GreaterThanThreshold
```

Hay que confirmar la suscripción desde el correo que manda SNS.

### 15.4 Cambiar un secreto

1. `aws secretsmanager put-secret-value --secret-id <nombre> --secret-string file://<json>`, desde una carpeta temporal como la de la Fase 6, que se borra después.
2. `kubectl annotate externalsecret <nombre> -n $NS force-sync=$(date +%s) --overwrite`
3. `kubectl rollout restart deployment/<servicio> -n $NS`, en cada servicio que use ese secreto (tabla de la sección 8.1).

La llave JWT tiene su propio procedimiento en `docs/deployment/rotacion-llaves-jwt.md`:
- Se cambian juntas la privada (`jwt-firma`) y la pública (`JWT_PUBLICA` en el ConfigMap `comun`).
- Se reinician primero signupapi y adminapi, y después el resto.
- Todas las sesiones se cierran. Las apps MAUI descargan la nueva llave del JWKS solas.

### 15.5 Copias de seguridad

- **RDS:** copias automáticas de 14 días, y además una instantánea manual antes de cada despliegue que traiga migraciones: `aws rds create-db-snapshot --db-instance-identifier mlm-prod-sql --db-snapshot-identifier mlm-prod-sql-antes-$TAG`.
- **EFS:** la política de copia activada en la Fase 5 la incluye en el plan diario por defecto de AWS Backup.
- **Secretos:** Secrets Manager guarda las versiones anteriores. El PFX y su contraseña además están en el gestor de contraseñas (Fase 6).
- **Prueba de restauración:** una vez al mes, restaurar la última instantánea de RDS en una instancia temporal y borrarla después.

---

## 16. Fase 14 — Encender lo que quedó apagado

Cada punto se hace por separado y se comprueba antes del siguiente.

### 16.1 Entrax

1. Pedir a Entrax el `KeyId` y el `Secret` de producción, y sus IP de salida.
2. Ponerlos en `mlmconqueror/prod/signupapi` (sección 15.4).
3. En el ConfigMap `signupapi`, poner `Entrax__Enabled: "true"` y aplicarlo con `aplicar ~/mlm-k8s/configmaps.yaml`.
4. Reiniciar signupapi.
5. Darle a Entrax la URL `https://auth-api.${DOMINIO}/api/v1/signups/entrax/callback`.
6. **Comprobar:** un POST sin firma a esa URL responde 401, y el trabajo `entrax-signup-sweep` aparece en el panel de SignupAPI.

### 16.2 SMS con Twilio y 2FA obligatorio

Se sigue `docs/deployment/verificacion-2fa.md`, en ese orden. Los pasos de EKS son estos:

1. Crear `mlmconqueror/prod/twilio` con `Notifications__Sms__Twilio__AccountSid` y `Notifications__Sms__Twilio__AuthToken`.
2. Agregar `- extract: {key: mlmconqueror/prod/twilio}` al `dataFrom` de los ExternalSecret `signupapi`, `adminapi`, `billing`, `bizcenter` y `datos`, y aplicarlo con `aplicar ~/mlm-k8s/externalsecrets.yaml`.
3. En el ConfigMap `comun`, poner `Notifications__Sms__Provider: Twilio` y `Notifications__Sms__Twilio__FromNumber`.
4. Antes de exigir el 2FA, purgar o cambiar las cuentas SuperAdmin cuyo correo sea de un buzón público. Si no, el código del 2FA llega a un buzón que puede leer cualquiera.
5. Agregar la misma lista `Auth__TwoFactor__MandatoryRoles__0`, `__1`... a los ConfigMaps de signupapi, adminweb y bizcenterweb. Tiene que ser idéntica en los tres.
6. Reiniciar los tres servicios.

### 16.3 Pago con tarjeta

`Signup__CardEnabled`, `Membership__UpgradeEnabled` y los dos `CardEnabled` de Billing se quedan en `false` hasta que exista el cobro real con 3DS de Spreedly. Hoy el código es un simulador, y encenderlo activa membresías sin cobrar.

---

## 17. Fase 15 — Despliegue continuo con GitHub Actions

### 17.1 Confianza entre GitHub y AWS (OIDC)

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

### 17.2 Entorno de GitHub

En GitHub, en Settings, Environments, se crea `production`:
- **Required reviewers:** Nick. Ningún despliegue sale sin su aprobación.
- **Deployment branches:** solo `master`.
- **Variables del entorno:** `AWS_REGION`, `AWS_ACCOUNT_ID` y `EKS_CLUSTER` (`mlmconqueror-prod`).

### 17.3 Flujo `.github/workflows/deploy.yml`

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

El flujo no ejecuta las pruebas. Varias suites necesitan un SQL Server propio y la solución incluye las apps MAUI, que no compilan en Linux. Las pruebas se corren en Windows antes de lanzarlo, igual que en la sección 2.6. El flujo se lanza a mano (Actions, deploy, Run workflow) sobre `master`.

El nombre del repositorio ECR coincide con el proyecto en minúsculas (`SignupAPI` da `signupapi`), por eso basta con `tr`.

**Antes de cada despliegue que traiga migraciones** hay que hacer, a mano y en este orden:
1. La instantánea de RDS (sección 15.5).
2. Aplicar el `migrate-rds.sql` regenerado (Fase 8, secciones 10.2 a 10.4).
3. Lanzar el flujo.

El flujo no migra la base a propósito.

**Deshacer un despliegue:**

```
kubectl rollout undo deployment/<servicio> -n $NS
```

Una migración aplicada no se deshace con esto. Para eso se restaura la instantánea tomada antes.

---

## 18. Apps MAUI (fuera de EKS, pero dependen de él)

Antes de publicar las apps en las tiendas:

| App | Archivo | Cambio |
|---|---|---|
| AdminApp | `MauiProgram.cs` líneas 49 y 55 | `https://localhost:7005` pasa a `https://auth-api.${DOMINIO}` (login y JWKS) |
| AdminApp | `MauiProgram.cs` líneas 80 y 88 | `https://localhost:7002` pasa a `https://admin-api.${DOMINIO}` |
| BizCenterApp | `MauiProgram.cs`, `AuthApiBaseUrl` por defecto | `https://auth-api.${DOMINIO}` |
| BizCenterApp | `MauiProgram.cs`, `SignupAppUrl` por defecto | `https://signup.${DOMINIO}/ambassador-join` |
| BizCenterApp | `MauiProgram.cs` líneas 58 y 78 | `https://localhost:7001` pasa a `https://api.${DOMINIO}`. Hoy apunta al puerto de AdminWeb, no al de BizCenter. |

---

## 19. Lista final de comprobación

| # | Comprobación | Cómo |
|---|---|---|
| 1 | Los 7 cambios de la Fase 0 están en el commit desplegado | `git log` y la tabla de la sección 2 |
| 2 | 11 servicios en Running, 2 réplicas cada uno | `kubectl get deploy -n $NS` |
| 3 | Salud pública 200 en los 6 nombres con `/health` | Sección 14.3 |
| 4 | Swagger, Hangfire y `/api/v1/dev` responden 404 desde fuera | `curl` contra cada nombre |
| 5 | JWKS publica la llave nueva | `curl https://auth-api.${DOMINIO}/.well-known/jwks.json` |
| 6 | La sesión sobrevive a un reinicio del portal | Sección 14.4, punto 6 |
| 7 | SES fuera del sandbox y un correo real entregado | Secciones 7.7 y 14.4 |
| 8 | Los trabajos de Hangfire tienen servidor y próxima ejecución | Sección 15.2 |
| 9 | Alarmas creadas y suscripción confirmada | Sección 15.3 |
| 10 | Instantánea de RDS tras el esquema y copia de EFS activa | Secciones 10.5 y 7.5 |
| 11 | PFX y contraseña guardados fuera de AWS | Sección 8.4 |
| 12 | Interruptores de tarjeta en `false` y 2FA sin roles obligatorios hasta la Fase 14 | `kubectl get cm -n $NS -o yaml` |
| 13 | Credenciales expuestas en el repositorio rotadas | Sección 2.7 |
