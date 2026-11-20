#!/usr/bin/env bash
# Permiso para que un usuario o grupo de IAM (o un permission set de IAM Identity Center, pegando
# el JSON) lea todo mlmconqueror/local/* y nada de staging ni prod.
# Uso: bash deploy/aws/politica-desarrollador-local.sh local <grupo-iam>
source "$(dirname "$0")/comun.sh" "$1"
[ "$ENTORNO" = local ] || { echo "Solo para el entorno local" >&2; exit 1; }
GRUPO="${2:?falta el grupo de IAM}"
jq -n --arg arn "arn:aws:secretsmanager:$AWS_REGION:$CUENTA:secret:$PREFIJO/*" --arg kms "$(arn_kms)" '{
  Version:"2012-10-17", Statement:[
    {Effect:"Allow", Action:"secretsmanager:GetSecretValue", Resource:$arn},
    {Effect:"Allow", Action:"kms:Decrypt", Resource:$kms}]}' > "$DIR_AWS/.politica-local.json"
aws iam put-group-policy --group-name "$GRUPO" --policy-name mlmconqueror-local-secretos \
  --policy-document "$(archivo_aws "$DIR_AWS/.politica-local.json")"
rm -f "$DIR_AWS/.politica-local.json"
echo "El grupo $GRUPO puede leer $PREFIJO/*"
