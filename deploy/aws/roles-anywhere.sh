#!/usr/bin/env bash
# Autoridad certificadora propia del entorno, trust anchor y perfil de IAM Roles Anywhere.
# La llave privada de la autoridad queda en deploy/aws/ca/<entorno>/ (ignorado por git) y NO
# se copia al servidor Docker: guardala fuera de esa maquina (por ejemplo, en un USB cifrado).
# Uso: bash deploy/aws/roles-anywhere.sh <entorno>
source "$(dirname "$0")/comun.sh" "$1"
CA="$DIR_AWS/ca/$ENTORNO"; mkdir -p "$CA"; chmod 700 "$CA"

if [ ! -f "$CA/ca.key" ]; then
  openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
    -keyout "$CA/ca.key" -out "$CA/ca.crt" -subj "/CN=mlmconqueror $ENTORNO CA" \
    -addext "basicConstraints=critical,CA:TRUE" -addext "keyUsage=critical,keyCertSign,cRLSign"
  chmod 600 "$CA/ca.key"
fi

ancla=$(aws rolesanywhere list-trust-anchors \
  --query "trustAnchors[?name=='mlmconqueror-$ENTORNO'].trustAnchorArn | [0]" --output text)
if [ "$ancla" = "None" ]; then
  # El PEM tiene saltos de linea: se pasa como JSON, no en la sintaxis abreviada del CLI.
  jq -n --rawfile pem "$CA/ca.crt" '{sourceType:"CERTIFICATE_BUNDLE", sourceData:{x509CertificateData:$pem}}' > "$CA/fuente.json"
  ancla=$(aws rolesanywhere create-trust-anchor --name "mlmconqueror-$ENTORNO" --enabled \
    --source "$(archivo_aws "$CA/fuente.json")" \
    --query trustAnchor.trustAnchorArn --output text)
  rm -f "$CA/fuente.json"
fi

roles=$(for s in $(servicios); do echo "arn:aws:iam::$CUENTA:role/$(rol_de "$s")"; done | paste -sd' ')
perfil=$(aws rolesanywhere list-profiles \
  --query "profiles[?name=='mlmconqueror-$ENTORNO'].profileArn | [0]" --output text)
if [ "$perfil" = "None" ]; then
  perfil=$(aws rolesanywhere create-profile --name "mlmconqueror-$ENTORNO" --enabled \
    --duration-seconds 3600 --role-arns $roles --query profile.profileArn --output text)
else
  aws rolesanywhere update-profile --profile-id "${perfil##*/}" --role-arns $roles >/dev/null
fi

echo "TRUST_ANCHOR_ARN=$ancla"
echo "PROFILE_ARN=$perfil"
