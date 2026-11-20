#!/usr/bin/env bash
# Genera deploy/docker/.env para el docker-compose LOCAL. Solo trae la contrasena del SQL Server
# de prueba del propio compose: todo lo demas (llave JWT, PFX de las pasarelas, cuenta raiz, API
# keys, licencias) llega de AWS Secrets Manager, mlmconqueror/local/*, con tu perfil de AWS.
# Uso (Git Bash o WSL, desde la raiz del repositorio):  bash deploy/docker/generar-secretos.sh
set -euo pipefail
cd "$(dirname "$0")"

if [ -f .env ]; then
  echo "deploy/docker/.env ya existe. Borralo si quieres regenerarlo (el SQL Server de prueba" >&2
  echo "guarda la contrasena en su volumen: tambien habria que borrar el volumen sqldata)." >&2
  exit 1
fi

cat > .env <<EOF
SA_PASSWORD=Mlm-$(openssl rand -hex 12)-Aa1
EOF

echo "Listo: deploy/docker/.env"
echo "Antes de levantar el compose, comprueba tu perfil de AWS:"
echo "  aws secretsmanager describe-secret --secret-id mlmconqueror/local/datos --query Name"
