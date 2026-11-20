#!/usr/bin/env bash
# Escribe el token de Cloudflare Tunnel (secreto mlmconqueror/<entorno>/borde) en
# deploy/docker/secretos/tunel-token, de solo lectura, para cloudflared. Es el unico secreto
# que queda en disco en el servidor: solo sirve para hacerse pasar por el tunel y se revoca
# desde Cloudflare. Se corre en el servidor con la credencial del administrador, justo antes de up.
# Uso: bash deploy/docker/bajar-token-tunel.sh <staging|prod>
set -euo pipefail
ENTORNO="${1:?uso: $0 <staging|prod>}"
cd "$(dirname "$0")"
umask 077; mkdir -p secretos
aws secretsmanager get-secret-value --secret-id "mlmconqueror/$ENTORNO/borde" \
  --query SecretString --output text | jq -er '.Cloudflare__TunnelToken | select(length > 0)' > secretos/tunel-token.tmp \
  || { rm -f secretos/tunel-token.tmp; echo "El secreto borde no tiene token: cargalo primero." >&2; exit 1; }
mv secretos/tunel-token.tmp secretos/tunel-token
chmod 444 secretos/tunel-token   # cloudflared corre con otro usuario
echo "Token del tunel escrito en deploy/docker/secretos/tunel-token"
