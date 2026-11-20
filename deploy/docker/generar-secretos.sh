#!/usr/bin/env bash
# Genera deploy/docker/.env y deploy/docker/secrets/gateway.pfx para el docker-compose LOCAL.
# Son secretos de prueba para una sola maquina: nunca se usan en produccion (alli van en
# AWS Secrets Manager, ver docs/deployment/despliegue-aws-eks.md, Fase 6).
# Uso (Git Bash o WSL, desde la raiz del repositorio):  bash deploy/docker/generar-secretos.sh
set -euo pipefail
export MSYS_NO_PATHCONV=1           # Git Bash: que no convierta "/CN=..." en una ruta de Windows
cd "$(dirname "$0")"

if [ -f .env ]; then
  echo "deploy/docker/.env ya existe. Borralo (y secrets/gateway.pfx) si quieres regenerarlos." >&2
  exit 1
fi

mkdir -p secrets
tmp=secrets/.tmp   # relativa: openssl de Windows no entiende /tmp de Git Bash
rm -rf "$tmp" && mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT

# Llaves JWT: RSA 2048 en PKCS#8 DER base64, igual que en produccion.
# Se pasa por "pkcs8 -topk8": algunos openssl (el de Git para Windows, por ejemplo) sacan la
# llave en PKCS#1 aunque se use genpkey, y SignupAPI solo acepta PKCS#8.
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$tmp/priv.pem" 2>/dev/null
openssl pkcs8 -topk8 -nocrypt -in "$tmp/priv.pem" -outform DER -out "$tmp/priv.der"
openssl rsa -inform DER -in "$tmp/priv.der" -pubout -outform DER -out "$tmp/pub.der" 2>/dev/null

# Certificado del anillo de credenciales de las pasarelas (AdminAPI, Billing y BizCenter).
pfx_pass=$(openssl rand -hex 16)
openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
  -keyout "$tmp/dp.key" -out "$tmp/dp.crt" \
  -subj "/CN=MLMConqueror Gateway Credentials (docker local)" \
  -addext "keyUsage=critical,keyEncipherment,dataEncipherment" 2>/dev/null
openssl pkcs12 -export -inkey "$tmp/dp.key" -in "$tmp/dp.crt" -out secrets/gateway.pfx \
  -passout "pass:$pfx_pass" -certpbe AES-256-CBC -keypbe AES-256-CBC -macalg sha256

cat > .env <<EOF
SA_PASSWORD=Mlm-$(openssl rand -hex 12)-Aa1
JWT_PRIVATE=$(base64 -w0 "$tmp/priv.der")
JWT_PUBLIC=$(base64 -w0 "$tmp/pub.der")
PFX_PASSWORD=$pfx_pass
ROOT_EMAIL=root@mlmconqueror.local
ROOT_PASSWORD=Root-$(openssl rand -hex 8)-Aa1
EXTERNAL_API_KEY=$(openssl rand -hex 32)
SYNCFUSION_LICENSE_KEY=
EOF

echo "Listo: deploy/docker/.env y deploy/docker/secrets/gateway.pfx"
echo "Cuenta raiz: root@mlmconqueror.local / contrasena en ROOT_PASSWORD de deploy/docker/.env"
