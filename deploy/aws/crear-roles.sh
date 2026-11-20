#!/usr/bin/env bash
# Un rol por servicio con permiso solo sobre sus secretos (y S3/SES si los usa).
# Uso: bash deploy/aws/crear-roles.sh <entorno> eks  <cluster> <namespace>
#      bash deploy/aws/crear-roles.sh <entorno> docker
# En modo docker, roles-anywhere.sh tiene que haberse corrido antes (lee el ARN del trust anchor).
source "$(dirname "$0")/comun.sh" "$1"
MODO="${2:?falta el modo: eks o docker}"
KMS="$(arn_kms)"
BUCKET="${BUCKET:-}"; SES_FROM="${SES_FROM:-}"
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

confianza() {  # confianza <Servicio> <nombre-k8s>
  case "$MODO" in
    eks)
      local cluster="${3:?cluster}" ns="${4:?namespace}"
      local oidc; oidc=$(aws eks describe-cluster --name "$cluster" \
        --query 'cluster.identity.oidc.issuer' --output text | sed 's#https://##')
      jq -n --arg arn "arn:aws:iam::$CUENTA:oidc-provider/$oidc" --arg oidc "$oidc" \
            --arg sub "system:serviceaccount:$ns:$2" '{
        Version:"2012-10-17", Statement:[{Effect:"Allow", Principal:{Federated:$arn},
          Action:"sts:AssumeRoleWithWebIdentity",
          Condition:{StringEquals:{($oidc+":sub"):$sub, ($oidc+":aud"):"sts.amazonaws.com"}}}]}' ;;
    docker)
      local ancla; ancla=$(aws rolesanywhere list-trust-anchors \
        --query "trustAnchors[?name=='mlmconqueror-$ENTORNO'].trustAnchorArn | [0]" --output text)
      [ "$ancla" != "None" ] || { echo "Falta el trust anchor: corre roles-anywhere.sh $ENTORNO" >&2; exit 1; }
      jq -n --arg ancla "$ancla" --arg cn "$2" '{
        Version:"2012-10-17", Statement:[{Effect:"Allow", Principal:{Service:"rolesanywhere.amazonaws.com"},
          Action:["sts:AssumeRole","sts:TagSession","sts:SetSourceIdentity"],
          Condition:{StringEquals:{"aws:PrincipalTag/x509Subject/CN":$cn},
                     ArnEquals:{"aws:SourceArn":$ancla}}}]}' ;;
    *) echo "Modo no valido: $MODO" >&2; exit 1 ;;
  esac
}

for s in $(servicios); do
  rol=$(rol_de "$s"); nombre=$(echo "$s" | tr '[:upper:]' '[:lower:]')
  mapfile -t secretos < <(secretos_de "$s"); mapfile -t extra < <(aws_de "$s")

  confianza "$s" "$nombre" "${3:-}" "${4:-}" > "$tmp/confianza.json"
  if aws iam get-role --role-name "$rol" >/dev/null 2>&1; then
    aws iam update-assume-role-policy --role-name "$rol" --policy-document "$(archivo_aws "$tmp/confianza.json")"
  else
    aws iam create-role --role-name "$rol" --assume-role-policy-document "$(archivo_aws "$tmp/confianza.json")" \
      --tags Key=proyecto,Value=mlmconqueror Key=entorno,Value="$ENTORNO" >/dev/null
  fi

  arns=$(for x in "${secretos[@]}"; do arn_secreto "$x"; done | jq -R . | jq -s .)
  jq -n --argjson arns "$arns" --arg kms "$KMS" --arg bucket "$BUCKET" --arg from "$SES_FROM" \
        --argjson s3 "$(printf '%s\n' "${extra[@]}" | grep -qx s3 && echo true || echo false)" \
        --argjson ses "$(printf '%s\n' "${extra[@]}" | grep -qx ses && echo true || echo false)" '{
    Version:"2012-10-17", Statement:(
      (if ($arns|length) > 0 then [
        {Effect:"Allow", Action:"secretsmanager:GetSecretValue", Resource:$arns},
        {Effect:"Allow", Action:"kms:Decrypt", Resource:$kms,
         Condition:{StringEquals:{"kms:ViaService":"secretsmanager.'"$AWS_REGION"'.amazonaws.com"}}}] else [] end)
      + (if $s3 and $bucket != "" then [
        {Effect:"Allow", Action:["s3:GetObject","s3:PutObject","s3:DeleteObject"], Resource:("arn:aws:s3:::"+$bucket+"/*")},
        {Effect:"Allow", Action:"s3:ListBucket", Resource:("arn:aws:s3:::"+$bucket)}] else [] end)
      + (if $ses and $from != "" then [
        {Effect:"Allow", Action:["ses:SendEmail","ses:SendRawEmail"], Resource:"*",
         Condition:{StringEquals:{"ses:FromAddress":$from}}}] else [] end))}' > "$tmp/permisos.json"

  if [ "$(jq '.Statement | length' "$tmp/permisos.json")" -gt 0 ]; then
    aws iam put-role-policy --role-name "$rol" --policy-name secretos-y-aws \
      --policy-document "$(archivo_aws "$tmp/permisos.json")"
  else
    aws iam delete-role-policy --role-name "$rol" --policy-name secretos-y-aws 2>/dev/null || true
  fi
  echo "$rol: ${#secretos[@]} secreto(s); aws: ${extra[*]:-ninguno}"
done
