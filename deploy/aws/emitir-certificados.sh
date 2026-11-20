#!/usr/bin/env bash
# Un certificado por servicio (CN = nombre del servicio en minusculas), valido un año, firmado
# por la autoridad del entorno. Deja en deploy/aws/certificados/<entorno>/<servicio>/ cert.pem y
# key.pem para copiar al servidor Docker.
# Uso: bash deploy/aws/emitir-certificados.sh <entorno> [servicio]
source "$(dirname "$0")/comun.sh" "$1"
CA="$DIR_AWS/ca/$ENTORNO"; [ -f "$CA/ca.key" ] || { echo "Falta la autoridad: roles-anywhere.sh" >&2; exit 1; }
SALIDA="$DIR_AWS/certificados/$ENTORNO"

for s in ${2:-$(servicios)}; do
  [ -n "$(secretos_de "$s")$(aws_de "$s")" ] || { echo "$s: no usa AWS, sin certificado"; continue; }
  n=$(echo "$s" | tr '[:upper:]' '[:lower:]'); d="$SALIDA/$n"; mkdir -p "$d"; chmod 700 "$d"
  openssl req -new -newkey rsa:2048 -nodes -keyout "$d/key.pem" -out "$d/csr.pem" -subj "/CN=$n" 2>/dev/null
  # Archivo y no <(...): el openssl de Windows no lee sustituciones de proceso de Git Bash.
  printf "basicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature\n" > "$d/ext.cnf"
  openssl x509 -req -in "$d/csr.pem" -CA "$CA/ca.crt" -CAkey "$CA/ca.key" -CAcreateserial \
    -days 365 -sha256 -out "$d/cert.pem" -extfile "$d/ext.cnf" 2>/dev/null
  rm -f "$d/csr.pem" "$d/ext.cnf"; chmod 600 "$d/key.pem"
  echo "$n: $(openssl x509 -in "$d/cert.pem" -noout -enddate)"
done
