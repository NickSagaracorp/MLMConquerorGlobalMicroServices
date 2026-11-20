#!/usr/bin/env bash
# Genera, junto a cada .md de docs/superpowers, docs/deployment y docs/arquitectura, su PDF
# oficial con la marca de Sagara Media Group. El titulo es la primera linea "# " del .md.
# Las guias de docs/deployment usan codigo mas pequeno (7.4) para que los comandos quepan.
# Uso (desde la raiz):  bash docs/generar-pdfs.sh [archivo.md ...]
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
archivos=("$@")
[ ${#archivos[@]} -gt 0 ] || mapfile -t archivos < <(git ls-files 'docs/superpowers/*.md' 'docs/deployment/*.md' 'docs/arquitectura/*.md')
fallos=0
for md in "${archivos[@]}"; do
  titulo=$(grep -m1 '^# ' "$md" | sed 's/^# //' || true)
  [ -n "$titulo" ] || titulo=$(basename "$md" .md)
  case "$md" in docs/deployment/*) codigo=7.4 ;; *) codigo=8.3 ;; esac
  if python -I docs/build_pdf.py "$md" "${md%.md}.pdf" "$titulo" "$codigo" >/dev/null 2>"${TMPDIR:-/tmp}/generar-pdfs.err"; then
    echo "ok     ${md%.md}.pdf"
  else
    echo "FALLA  $md: $(tail -1 "${TMPDIR:-/tmp}/generar-pdfs.err")"; fallos=$((fallos+1))
  fi
done
[ "$fallos" -eq 0 ] || { echo "$fallos documento(s) con error" >&2; exit 1; }
