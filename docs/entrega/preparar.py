# -*- coding: utf-8 -*-
"""Prepara y revisa la copia exportada que se entrega al cliente.

  python preparar.py preparar <copia> <no-viaja.txt> <gitignore-cliente>
  python preparar.py revisar  <copia> <no-viaja.txt> <excepciones.txt>

revisar termina con codigo 1 y lista los problemas si encuentra algo que no debe viajar.
"""
import fnmatch, pathlib, re, shutil, sys

VIGILADAS = re.compile(r"claude|anthropic|superpowers|docs/(?:deployment|arquitectura|presentations)|UI-Templates|[A-Za-z0-9_-]+\.md\b", re.IGNORECASE)
OFFICE = (".pptx", ".docx", ".xlsx")
INTERNALS_CSPROJ = re.compile(r'^\s*<InternalsVisibleTo\s+Include="[^"]*\.Tests"\s*/>\s*\r?\n', re.MULTILINE)
INTERNALS_CS = re.compile(r'^\s*\[assembly:\s*InternalsVisibleTo\("[^"]*\.Tests"\)\]\s*\r?\n', re.MULTILINE)
PROYECTO_PRUEBAS = re.compile(r'^\s*<Project\s+Path="[^"]*\.Tests/[^"]*"\s*/>\s*\r?\n', re.MULTILINE)


def leer_patrones(archivo):
    return [l.strip() for l in pathlib.Path(archivo).read_text(encoding="utf-8").splitlines()
            if l.strip() and not l.strip().startswith("#")]


def coincide(rel, patrones):
    """rel: ruta relativa con '/'. Ver el encabezado de no-viaja.txt."""
    partes = rel.split("/")
    for p in patrones:
        if p.endswith("/"):
            carpeta = p[:-1]
            if "/" in carpeta:
                if any(fnmatch.fnmatch("/".join(partes[:i]), carpeta) for i in range(1, len(partes) + 1)):
                    return True
            elif any(fnmatch.fnmatch(x, carpeta) for x in partes[:-1]) or fnmatch.fnmatch(partes[0], carpeta):
                return True
        elif "/" in p:
            if fnmatch.fnmatch(rel, p):
                return True
        elif any(fnmatch.fnmatch(x, p) for x in partes):
            return True
    return False


def archivos(raiz):
    for p in sorted(pathlib.Path(raiz).rglob("*")):
        if p.is_file():
            yield p, p.relative_to(raiz).as_posix()


def reescribir(p, patron):
    texto = p.read_text(encoding="utf-8-sig")
    nuevo = patron.sub("", texto)
    if nuevo != texto:
        p.write_text(nuevo, encoding="utf-8")


def preparar(raiz, no_viaja, gitignore_cliente):
    raiz = pathlib.Path(raiz)
    for p, rel in list(archivos(raiz)):
        if coincide(rel, no_viaja):
            p.unlink()
    for d in sorted((d for d in raiz.rglob("*") if d.is_dir()), key=lambda d: len(d.parts), reverse=True):
        if not any(d.iterdir()):
            d.rmdir()
    for slnx in raiz.glob("*.slnx"):
        reescribir(slnx, PROYECTO_PRUEBAS)
    for csproj in raiz.rglob("*.csproj"):
        reescribir(csproj, INTERNALS_CSPROJ)
    for cs in raiz.rglob("*.cs"):
        if "InternalsVisibleTo" in cs.read_text(encoding="utf-8-sig", errors="ignore"):
            reescribir(cs, INTERNALS_CS)
    shutil.copyfile(gitignore_cliente, raiz / ".gitignore")


def texto_de_office(p):
    """El texto de un .pptx/.docx/.xlsx (son zip de XML), para revisarlo como cualquier otro."""
    import zipfile
    with zipfile.ZipFile(p) as z:
        return "\n".join(z.read(n).decode("utf-8", errors="ignore")
                         for n in z.namelist() if n.endswith(".xml"))


def revisar(raiz, no_viaja, excepciones):
    problemas = []
    for p, rel in archivos(raiz):
        if coincide(rel, no_viaja):
            problemas.append(f"no debe viajar: {rel}")
            continue
        if coincide(rel, excepciones):
            continue
        if rel.lower().endswith(OFFICE):
            texto = texto_de_office(p)
        else:
            datos = p.read_bytes()
            if b"\x00" in datos[:8000]:
                continue  # binario
            texto = datos.decode("utf-8", errors="ignore")
        if rel.endswith((".slnx", ".sln", ".csproj")):
            for m in re.finditer(r"[\w.]+\.Tests\b", texto):
                problemas.append(f"referencia a pruebas ({m.group(0)}): {rel}")
                break
        m = VIGILADAS.search(texto)
        if m:
            linea = texto.count("\n", 0, m.start()) + 1
            problemas.append(f"palabra vigilada '{m.group(0)}': {rel}:{linea}")
    return problemas


def main(argv):
    modo, copia, lista, extra = argv[1:5]
    if modo == "preparar":
        preparar(copia, leer_patrones(lista), extra)
        return 0
    if modo == "revisar":
        problemas = revisar(copia, leer_patrones(lista), leer_patrones(extra))
        for p in problemas:
            print("  " + p)
        return 1 if problemas else 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
