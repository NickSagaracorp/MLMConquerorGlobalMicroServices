#!/usr/bin/env bash
# Arma una entrega para el remoto del cliente: una foto sin historial del commit indicado, sin
# nada de docs/entrega/no-viaja.txt. NO empuja: deja la entrega lista en una carpeta y dice
# como empujarla. Empujar es una segunda llamada explicita, solo con el OK de Nick.
#
#   bash docs/entrega/entregar.sh armar   <rama-o-commit> <nombre-del-remoto> [rama-remota]
#   bash docs/entrega/entregar.sh empujar <carpeta-que-dio-armar>
# Ejemplo (MWRLife):  bash docs/entrega/entregar.sh armar MWRLife mwrlife
# El remoto tiene el pushurl anulado; aqui se usa su url real desde un clon propio.
set -euo pipefail
RAIZ="$(git rev-parse --show-toplevel)"; DIR="$RAIZ/docs/entrega"
# En Git Bash de Windows, git y dotnet son programas de Windows: las rutas que se les pasan van en
# forma C:/... (cygpath -m). En Linux no hace falta.
ruta() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }
SLN="MLMConquerorGlobalEdition.slnx"

armar() {
  local commit="${1:?falta la rama o el commit}" nombre="${2:?falta el nombre del remoto}" rama="${3:-main}"
  local remoto; remoto=$(git -C "$RAIZ" config --get "remote.$nombre.url") \
    || { echo "No existe el remoto $nombre" >&2; return 1; }
  local sha; sha=$(git -C "$RAIZ" rev-parse --verify "$commit^{commit}")
  local fecha; fecha=$(date +%F)
  local base; base="$(ruta "$HOME")/mlm-entregas/$(date +%Y%m%d-%H%M%S)"
  mkdir -p "$base"
  echo "Entrega de ${sha:0:7} hacia $remoto ($rama) en $base"

  echo "1/6 Pruebas en el repositorio privado, sobre ese commit"
  git -C "$RAIZ" worktree add --detach "$base/fuente" "$sha" >/dev/null
  trap 'git -C "$RAIZ" worktree remove --force "$base/fuente" 2>/dev/null || true' RETURN
  ls -d "$base"/fuente/*.Tests >/dev/null 2>&1 || { echo "No se encontraron proyectos de pruebas en $base/fuente." >&2; return 1; }
  for t in "$base"/fuente/*.Tests; do
    salida=$(dotnet test "$t" -nologo -v q 2>&1 || true)
    r=$(echo "$salida" | grep -E "Passed!|Failed!" || true)
    echo "    $(basename "$t"): ${r:-sin resultado}"
    case "$r" in
      Passed!*) ;;
      *) echo "$salida" | grep -E "^\s+Failed |error " -A6 | head -40 >&2
         echo "Una suite no paso: no se entrega." >&2; return 1 ;;
    esac
  done

  echo "2/6 Exportar el arbol sin historial"
  mkdir -p "$base/copia"
  git -C "$RAIZ" archive "$sha" | tar -x -C "$base/copia"

  echo "3/6 Preparar"
  python -I "$DIR/preparar.py" preparar "$base/copia" "$DIR/no-viaja.txt" "$DIR/gitignore-cliente"

  echo "4/6 Revisar"
  if ! python -I "$DIR/preparar.py" revisar "$base/copia" "$DIR/no-viaja.txt" "$DIR/excepciones.txt"; then
    echo "La copia tiene cosas que no deben viajar (arriba). No se entrega." >&2; return 1
  fi

  echo "5/6 Compilar la copia"
  cp -r "$base/copia" "$base/compilar"
  dotnet build "$base/compilar/$SLN" -nologo -v q 2>&1 | grep -E "error|Error\(s\)" | tail -3
  dotnet build "$base/compilar/$SLN" -nologo -v q >/dev/null 2>&1 || { echo "La copia no compila. No se entrega." >&2; return 1; }
  rm -rf "$base/compilar"

  echo "6/6 Commit de entrega"
  if git ls-remote --exit-code --heads "$remoto" "$rama" >/dev/null 2>&1; then
    git clone -q --branch "$rama" --single-branch "$remoto" "$base/entrega"
  else
    git init -q "$base/entrega"; git -C "$base/entrega" checkout -q --orphan "$rama"
    git -C "$base/entrega" remote add origin "$remoto"
  fi
  find "$base/entrega" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
  cp -a "$base/copia/." "$base/entrega/"
  git -C "$base/entrega" add -A
  if git -C "$base/entrega" diff --cached --quiet; then
    echo "No hay cambios respecto de la entrega anterior."; return 0
  fi
  git -C "$base/entrega" commit -q -m "Entrega $fecha"
  echo
  git -C "$base/entrega" show --stat --oneline HEAD | tail -15
  echo
  echo "Lista para empujar. Revisa y, con el OK, corre:"
  echo "  bash docs/entrega/entregar.sh empujar \"$base\""
}

empujar() {
  local base="${1:?falta la carpeta}"
  local rama; rama=$(git -C "$base/entrega" rev-parse --abbrev-ref HEAD)
  git -C "$base/entrega" push origin "$rama"
}

case "${1:-}" in
  armar) shift; armar "$@" ;;
  empujar) shift; empujar "$@" ;;
  *) sed -n '2,9p' "$0"; exit 2 ;;
esac
