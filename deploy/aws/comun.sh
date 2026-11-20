#!/usr/bin/env bash
# Variables y funciones compartidas. Se carga con: source "$(dirname "$0")/comun.sh" "$ENTORNO"
set -euo pipefail
export MSYS_NO_PATHCONV=1

ENTORNO="${1:?uso: $0 <local|staging|prod> ...}"
case "$ENTORNO" in local|staging|prod) ;; *) echo "Entorno no valido: $ENTORNO" >&2; exit 1;; esac

AWS_REGION="${AWS_REGION:-us-east-1}"
CUENTA="$(aws sts get-caller-identity --query Account --output text)"
DIR_AWS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TABLA="$DIR_AWS/secretos-por-servicio.json"
PREFIJO="mlmconqueror/$ENTORNO"
ALIAS_KMS="alias/mlmconqueror-$ENTORNO"

# servicios: nombres de la tabla (AdminAPI, AdminWeb, ...)
servicios() { jq -r '.servicios | keys[]' "$TABLA"; }
# secretos_de <Servicio>
secretos_de() { jq -r --arg s "$1" '.servicios[$s].secretos[]' "$TABLA"; }
# aws_de <Servicio>: s3, ses
aws_de() { jq -r --arg s "$1" '.servicios[$s].aws[]' "$TABLA"; }
# rol_de <Servicio>: MLM-prod-billing
rol_de() { echo "MLM-$ENTORNO-$(echo "$1" | tr '[:upper:]' '[:lower:]')"; }
# arn_secreto <secreto>: ARN con comodin para el sufijo aleatorio de 6 caracteres que agrega AWS
arn_secreto() { echo "arn:aws:secretsmanager:$AWS_REGION:$CUENTA:secret:$PREFIJO/$1-??????"; }
# archivo_aws <ruta>: file://... que entiende el AWS CLI tambien en Windows (Git Bash).
archivo_aws() { if command -v cygpath >/dev/null 2>&1; then echo "file://$(cygpath -m "$1")"; else echo "file://$1"; fi; }
arn_kms() { aws kms describe-key --key-id "$ALIAS_KMS" --query KeyMetadata.Arn --output text; }
