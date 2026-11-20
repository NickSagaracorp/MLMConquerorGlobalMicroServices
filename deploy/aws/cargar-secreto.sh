#!/usr/bin/env bash
# Pone el valor de un secreto desde un archivo JSON local de pares "Seccion__Clave": "texto".
# Uso: bash deploy/aws/cargar-secreto.sh <entorno> <secreto> <archivo.json>
# Despues de cargarlo, borra el archivo (shred -u) y reinicia los servicios que lo leen.
source "$(dirname "$0")/comun.sh" "$1"
SECRETO="${2:?falta el secreto}"; ARCHIVO="${3:?falta el archivo JSON}"

jq -r '.secretos[]' "$TABLA" | grep -qx "$SECRETO" || { echo "$SECRETO no esta en la tabla" >&2; exit 1; }
jq -e 'type == "object" and ([.[] | type] | all(. == "string"))' "$ARCHIVO" >/dev/null \
  || { echo "$ARCHIVO tiene que ser un objeto JSON con valores de texto" >&2; exit 1; }

aws secretsmanager put-secret-value --secret-id "$PREFIJO/$SECRETO" \
  --secret-string "$(archivo_aws "$ARCHIVO")" >/dev/null
echo "cargado: $PREFIJO/$SECRETO ($(jq 'length' "$ARCHIVO") claves). Lo leen:"
for s in $(servicios); do secretos_de "$s" | grep -qx "$SECRETO" && echo "  $s"; done
