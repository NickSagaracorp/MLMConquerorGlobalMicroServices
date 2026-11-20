#!/usr/bin/env bash
# Crea la llave KMS del entorno y los secretos declarados en la tabla, vacios ({}).
# Uso: bash deploy/aws/crear-kms-y-secretos.sh <entorno>
source "$(dirname "$0")/comun.sh" "$1"

if ! aws kms describe-key --key-id "$ALIAS_KMS" >/dev/null 2>&1; then
  id=$(aws kms create-key --description "Secretos de mlmconqueror ($ENTORNO)" \
        --tags TagKey=proyecto,TagValue=mlmconqueror TagKey=entorno,TagValue="$ENTORNO" \
        --query KeyMetadata.KeyId --output text)
  aws kms create-alias --alias-name "$ALIAS_KMS" --target-key-id "$id"
  aws kms enable-key-rotation --key-id "$id"
  echo "Llave KMS $ALIAS_KMS creada"
fi

for s in $(jq -r '.secretos[]' "$TABLA"); do
  if aws secretsmanager describe-secret --secret-id "$PREFIJO/$s" >/dev/null 2>&1; then
    echo "ya existe: $PREFIJO/$s"
  else
    aws secretsmanager create-secret --name "$PREFIJO/$s" --kms-key-id "$ALIAS_KMS" \
      --secret-string '{}' --tags Key=proyecto,Value=mlmconqueror Key=entorno,Value="$ENTORNO" >/dev/null
    echo "creado: $PREFIJO/$s (vacio; cargalo con cargar-secreto.sh)"
  fi
done
